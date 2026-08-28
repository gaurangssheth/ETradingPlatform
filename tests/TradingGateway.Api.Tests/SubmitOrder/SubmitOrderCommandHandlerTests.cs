using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NServiceBus;
using NServiceBus.TransactionalSession;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;
using TradingApp.Shared.Validation;
using TradingGateway.Api.Application.Commands.SubmitOrder;
using TradingGateway.Api.Application.Commands.SubmitOrder.Validation;

namespace TradingGateway.Api.Tests.SubmitOrder
{
    public class SubmitOrderCommandHandlerTests
    {
        [Fact]
        public async Task HandleAsync_WhenCommandIsValid_ShouldOpenSessionSendSubmitOrderCommitAndReturnSubmitted()
        {
            var validatorFactory = CreateValidatorFactory();

            var transactionalSession = new Mock<ITransactionalSession>();

            TradingApp.Contracts.Commands.SubmitOrder? sentMessage = null;

            transactionalSession
            .Setup(x => x.Open(
                It.IsAny<SqlPersistenceOpenSessionOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            transactionalSession
                .Setup(x => x.Commit(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);


            transactionalSession
                .Setup(x => x.Send(
                    It.IsAny<object>(),
                    It.IsAny<SendOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, SendOptions, CancellationToken>((message, _, _) =>
                {
                    sentMessage = message.Should()
                                    .BeOfType<TradingApp.Contracts.Commands.SubmitOrder>()
                                    .Subject;
                })
                .Returns(Task.CompletedTask);

            var handler = new SubmitOrderCommandHandler(
                validatorFactory,
                transactionalSession.Object,
                NullLogger<SubmitOrderCommandHandler>.Instance);

            var command = new SubmitOrderCommand
            (
                "client-001",
                "EURUSD",
                "Buy",
                100000m,
                "Market",
                null,
                "gateway-handler-test-001"
            );

            var result = await handler.HandleAsync(command, CancellationToken.None);

            result.Accepted.Should().BeTrue();
            result.Status.Should().Be("Submitted");
            result.Error.Should().BeNull();
            result.CorrelationId.Should().Be("gateway-handler-test-001");
            result.OrderId.Should().NotBeEmpty();

            sentMessage.Should().NotBeNull();
            sentMessage!.OrderId.Should().Be(result.OrderId);
            sentMessage.ClientId.Should().Be("client-001");
            sentMessage.Symbol.Should().Be("EURUSD");
            sentMessage.Side.Should().Be(OrderSide.Buy);
            sentMessage.Quantity.Should().Be(100000m);
            sentMessage.OrderType.Should().Be(OrderType.Market);
            sentMessage.LimitPrice.Should().BeNull();
            sentMessage.CorrelationId.Should().Be("gateway-handler-test-001");

            transactionalSession.Verify(x => x.Open(
                    It.IsAny<SqlPersistenceOpenSessionOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            transactionalSession.Verify(x => x.Send(
                    It.IsAny<object>(),
                    It.IsAny<SendOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            transactionalSession.Verify(x => x.Commit(
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenCommandIsInvalid_ShouldReturnValidationFailedAndNotOpenSession()
        {
            var validatorFactory = CreateValidatorFactory();

            var transactionalSession = new Mock<ITransactionalSession>();

            var handler = new SubmitOrderCommandHandler(
                validatorFactory,
                transactionalSession.Object,
                NullLogger<SubmitOrderCommandHandler>.Instance);

            var command = new SubmitOrderCommand(
                ClientId: "",
                Symbol: "EURUSD",
                Side: "InvalidSide",
                Quantity: 100000m,
                OrderType: "Market",
                LimitPrice: null,
                CorrelationId: "gateway-command-handler-test-invalid");

            var result = await handler.HandleAsync(command, CancellationToken.None);

            result.Accepted.Should().BeFalse();
            result.Status.Should().Be("ValidationFailed");
            result.Error.Should().NotBeNullOrWhiteSpace();
            result.CorrelationId.Should().Be("gateway-command-handler-test-invalid");
            result.OrderId.Should().BeEmpty();

            transactionalSession.Verify(x => x.Open(
                    It.IsAny<SqlPersistenceOpenSessionOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            transactionalSession.Verify(x => x.Send(
                    It.IsAny<object>(),
                    It.IsAny<SendOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            transactionalSession.Verify(x => x.Commit(
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenLimitOrderHasNoLimitPrice_ShouldReturnValidationFailed()
        {
            var validatorFactory = CreateValidatorFactory();

            var tractionalSession = new Mock<ITransactionalSession>();

            var handler = new SubmitOrderCommandHandler(
                validatorFactory,
                tractionalSession.Object,
                NullLogger<SubmitOrderCommandHandler>.Instance);

            var command = new SubmitOrderCommand(
                ClientId: "client-001",
                Symbol: "EURUSD",
                Side: "Buy",
                Quantity: 100000m,
                OrderType: "Limit",
                LimitPrice: null,
                CorrelationId: "gateway-limit-no-price-001");

            var result = await handler.HandleAsync(command, CancellationToken.None);

            result.Accepted.Should().BeFalse();
            result.Status.Should().Be("ValidationFailed");
            result.Error.Should().NotBeNullOrWhiteSpace();

            tractionalSession.Verify(x => x.Open(
                    It.IsAny<SqlPersistenceOpenSessionOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenLimitOrderHasZeroLimitPrice_ShouldReturnValidationFailed()
        {
            var validatorFactory = CreateValidatorFactory();

            var transactionalSession =
                new Mock<ITransactionalSession>();

            var handler =
                new SubmitOrderCommandHandler(
                    validatorFactory,
                    transactionalSession.Object,
                    NullLogger<SubmitOrderCommandHandler>.Instance);

            var command =
                new SubmitOrderCommand(
                    ClientId: "client-001",
                    Symbol: "EURUSD",
                    Side: "Buy",
                    Quantity: 100000m,
                    OrderType: "Limit",
                    LimitPrice: 0m,
                    CorrelationId: "gateway-limit-zero-price-001");

            var result =
                await handler.HandleAsync(
                    command,
                    CancellationToken.None);

            result.Accepted.Should().BeFalse();
            result.Status.Should().Be("ValidationFailed");
            result.Error.Should()
                .Contain("LimitPrice must be greater than 0");

            transactionalSession.Verify(
                x => x.Open(
                    It.IsAny<SqlPersistenceOpenSessionOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_WhenLimitOrderHasPositiveLimitPrice_ShouldSubmitOrder()
        {
            var validatorFactory = CreateValidatorFactory();

            var transactionalSession = new Mock<ITransactionalSession>();

            TradingApp.Contracts.Commands.SubmitOrder? sentMessage = null;

            transactionalSession
            .Setup(x => x.Open(
                It.IsAny<SqlPersistenceOpenSessionOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

            transactionalSession
                .Setup(x => x.Commit(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);


            transactionalSession
                .Setup(x => x.Send(
                    It.IsAny<object>(),
                    It.IsAny<SendOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, SendOptions, CancellationToken>((message, _, _) =>
                {
                    sentMessage = message.Should()
                                    .BeOfType<TradingApp.Contracts.Commands.SubmitOrder>()
                                    .Subject;
                })
                .Returns(Task.CompletedTask);

            var handler =
                new SubmitOrderCommandHandler(
                    validatorFactory,
                    transactionalSession.Object,
                    NullLogger<SubmitOrderCommandHandler>.Instance);

            var command =
                new SubmitOrderCommand(
                    ClientId: "client-001",
                    Symbol: "EURUSD",
                    Side: "Buy",
                    Quantity: 100000m,
                    OrderType: "Limit",
                    LimitPrice: 1.0845m,
                    CorrelationId: "gateway-limit-valid-001");

            var result =
                await handler.HandleAsync(
                    command,
                    CancellationToken.None);

            result.Accepted.Should().BeTrue();
            result.Status.Should().Be("Submitted");

            sentMessage.Should().NotBeNull();
            sentMessage!.OrderType.Should().Be(OrderType.Limit);
            sentMessage.LimitPrice.Should().Be(1.0845m);
        }

        private static ValidatorFactory CreateValidatorFactory()
        {
            return new ValidatorFactory(new IPolymorphicValidator[]
            {
            new SubmitOrderCommandValidator()
            });
        }
    }
}
