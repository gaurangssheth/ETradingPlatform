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
using TradingGateway.Api.Validation;

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

        private static ValidatorFactory CreateValidatorFactory()
        {
            return new ValidatorFactory(new IPolymorphicValidator[]
            {
            new SubmitOrderCommandValidator()
            });
        }
    }
}
