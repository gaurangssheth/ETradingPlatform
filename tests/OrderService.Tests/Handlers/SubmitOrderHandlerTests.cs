using FluentAssertions;
using Grpc.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NServiceBus.Testing;
using OrderService.Domain;
using OrderService.Handlers;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Repositories;
using OrderService.Infrastructure.UnitOfWork;
using OrderService.Pricing;
using OrderService.Risk;
using OrderService.Sagas;
using OrderService.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;

namespace OrderService.Tests.Handlers
{
    public class SubmitOrderHandlerTests
    {
        [Fact]
        public async Task Handle_WhenRiskApprovesOrder_ShouldSaveOrderAndPublishOrderAccepted()
        {
            await using var connection = new SqliteConnection("Datasource=:memory:");
            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);
            await dbContext.Database.EnsureCreatedAsync();

            var riskDecisionId = Guid.NewGuid();

            var riskClient = CreateRiskClient(
                approved: true,
                reasonCode: "APPROVED",
                reason: "Order approved by risk checks.",
                riskDecisionId: riskDecisionId);

            var handler = CreateHandler(dbContext, riskClient);
            var messageContext = new TestableMessageHandlerContext();

            var orderId = Guid.NewGuid();

            var command = new SubmitOrder
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Quantity = 100000m,
                CorrelationId = "submit-order-handler-test-001"
            };

            await handler.Handle(command, messageContext);

            var order = await dbContext.Orders.SingleAsync(x => x.Id == command.OrderId);

            order.Id.Should().Be(orderId);
            order.ClientId.Should().Be("client-001");
            order.Symbol.Should().Be("EURUSD");
            order.Side.Should().Be(OrderSide.Buy);
            order.Quantity.Should().Be(100000m);
            order.OrderType.Should().Be(OrderType.Market);
            order.Status.Should().Be(OrderStatus.Accepted);
            order.LimitPrice.Should().BeNull();
            order.CorrelationId.Should().Be("submit-order-handler-test-001");
            order.CreatedAt.Should().NotBe(default);
            order.AcceptedAt.Should().NotBeNull();
            order.RejectedAt.Should().BeNull();

            messageContext.PublishedMessages.Should().ContainSingle();

            var published = messageContext.PublishedMessages.Single().Message
                .Should()
                .BeOfType<OrderAccepted>()
                .Subject;


            published.OrderId.Should().Be(orderId);
            published.ClientId.Should().Be("client-001");
            published.Symbol.Should().Be("EURUSD");
            published.Side.Should().Be(OrderSide.Buy);
            published.OrderType.Should().Be(OrderType.Market);
            published.Quantity.Should().Be(100000m);
            published.LimitPrice.Should().BeNull();
            published.CorrelationId.Should().Be("submit-order-handler-test-001");
            published.AcceptedAt.Should().NotBe(default);
            published.RiskDecisionId.Should().Be(riskDecisionId.ToString());

            riskClient.Verify(x => x.CheckOrderRiskAsync(
                It.Is<SubmitOrder>(o => o.OrderId == orderId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        }

        [Fact]
        public async Task Handle_WhenLimitOrderIsReceived_ShouldPersistLimitPrice()
        {
            await using var connection =
                new SqliteConnection("Datasource=:memory:");

            await connection.OpenAsync();

            await using var dbContext =
                CreateDbContext(connection);

            await dbContext.Database.EnsureCreatedAsync();

            var riskDecisionId = Guid.NewGuid();

            var riskClient = CreateRiskClient(
                approved: true,
                reasonCode: "APPROVED",
                reason: "Order approved by risk checks.",
                riskDecisionId: riskDecisionId);

            var handler =
                CreateHandler(dbContext, riskClient);

            var messageContext =
                new TestableMessageHandlerContext();

            var orderId = Guid.NewGuid();

            var command = new SubmitOrder
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                Quantity = 100000m,
                LimitPrice = 1.0845m,
                CorrelationId = "limit-order-persistence-test-001"
            };

            await handler.Handle(
                command,
                messageContext);

            var order =
                await dbContext.Orders.SingleAsync(
                    x => x.Id == orderId);

            order.OrderType.Should().Be(OrderType.Limit);
            order.LimitPrice.Should().Be(1.0845m);
            order.Status.Should().Be(OrderStatus.Accepted);

            messageContext.PublishedMessages.Should().ContainSingle();

            var published = messageContext.PublishedMessages.Single().Message
                .Should()
                .BeOfType<OrderAccepted>()
                .Subject;


            published.OrderId.Should().Be(orderId);
            published.ClientId.Should().Be("client-001");
            published.Symbol.Should().Be("EURUSD");
            published.Side.Should().Be(OrderSide.Buy);
            published.OrderType.Should().Be(OrderType.Limit);
            published.Quantity.Should().Be(100000m);
            published.LimitPrice.Should().Be(1.0845m);
            published.CorrelationId.Should().Be("limit-order-persistence-test-001");
            published.AcceptedAt.Should().NotBe(default);
            published.RiskDecisionId.Should().Be(riskDecisionId.ToString());
        }

        [Fact]
        public async Task Handle_WhenRiskRejectsOrder_ShouldSaveRejectedOrderAndPublishOrderRejected()
        {
            await using var connection = new SqliteConnection("Datasource=:memory:");
            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);
            await dbContext.Database.EnsureCreatedAsync();

            var riskDecisionId = Guid.NewGuid();

            var riskClient = CreateRiskClient(
                approved: false,
                reasonCode: "MAX_ORDER_SIZE_EXCEEDED",
                reason: "Quantity 1000001 exceeds maximum order size 1000000.",
                riskDecisionId: riskDecisionId);

            var handler = CreateHandler(dbContext, riskClient);
            var messageContext = new TestableMessageHandlerContext();

            var orderId = Guid.NewGuid();

            var command = new SubmitOrder
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Quantity = 1_000_001m,
                CorrelationId = "submit-order-handler-test-rejected"
            };

            await handler.Handle(command, messageContext);

            var order = await dbContext.Orders.SingleAsync(x => x.Id == command.OrderId);

            order.Id.Should().Be(orderId);
            order.ClientId.Should().Be("client-001");
            order.Symbol.Should().Be("EURUSD");
            order.Side.Should().Be(OrderSide.Buy);
            order.Quantity.Should().Be(1_000_001m);
            order.OrderType.Should().Be(OrderType.Market);
            order.Status.Should().Be(OrderStatus.Rejected);
            order.CorrelationId.Should().Be("submit-order-handler-test-rejected");
            order.CreatedAt.Should().NotBe(default);
            order.AcceptedAt.Should().BeNull();
            order.RejectedAt.Should().NotBeNull();
            order.RejectionReason.Should().Be("Quantity 1000001 exceeds maximum order size 1000000.");

            messageContext.PublishedMessages.Should().ContainSingle();

            var published = messageContext.PublishedMessages.Single().Message
                .Should()
                .BeOfType<OrderRejected>()
                .Subject;


            published.OrderId.Should().Be(orderId);
            published.ClientId.Should().Be("client-001");
            published.Symbol.Should().Be("EURUSD");
            published.Side.Should().Be(OrderSide.Buy);
            published.OrderType.Should().Be(OrderType.Market);
            published.Quantity.Should().Be(1_000_001m);
            published.Reason.Should().Be("Quantity 1000001 exceeds maximum order size 1000000.");
            published.CorrelationId.Should().Be("submit-order-handler-test-rejected");
            published.RejectedAt.Should().NotBe(default);
            published.RiskDecisionId.Should().Be(riskDecisionId.ToString());

            riskClient.Verify(x => x.CheckOrderRiskAsync(
                It.Is<SubmitOrder>(o => o.OrderId == orderId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        }

        [Fact]
        public async Task Handle_WhenRiskServiceFails_ShouldSaveOrderAsPendingRiskAndNotPublishEvent()
        {
            await using var connection = new SqliteConnection("Datasource=:memory:");
            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);
            await dbContext.Database.EnsureCreatedAsync();

            var riskClient = new Mock<IRiskClient>();

            riskClient.Setup(x => x.CheckOrderRiskAsync(
                    It.IsAny<SubmitOrder>(),
                    It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new RpcException(
                        new Status(
                            StatusCode.Unavailable,
                            "RiskService is unavailable.")));

            var handler = CreateHandler(dbContext, riskClient);
            var messageContext = new TestableMessageHandlerContext();
            var orderId = Guid.NewGuid();
            var command = new SubmitOrder
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Quantity = 100000m,
                CorrelationId = "submit-order-handler-test-exception"
            };

            Func<Task> action = () => handler.Handle(command, messageContext);

            var exception = await action.Should().ThrowAsync<RpcException>();

            exception.Which.StatusCode.Should().Be(StatusCode.Unavailable);
            exception.Which.Status.Detail.Should().Be("RiskService is unavailable.");


            var order = await dbContext.Orders.SingleAsync(x => x.Id == command.OrderId);
            order.Status.Should().Be(OrderStatus.PendingRisk);
            order.AcceptedAt.Should().BeNull();
            order.RejectedAt.Should().BeNull();
            order.RejectionReason.Should().BeNull();
            order.CorrelationId.Should().Be(command.CorrelationId);

            messageContext.PublishedMessages.Should().BeEmpty();

            riskClient.Verify(x => x.CheckOrderRiskAsync(
                It.Is<SubmitOrder>(o => o.OrderId == orderId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        }

        [Fact]
        public async Task Handle_WhenPendingRiskOrderAlreadyExists_ShouldReuseOrderandRetryRiskCheck()
        {
            await using var connection = new SqliteConnection("Datasource=:memory:");
            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);
            await dbContext.Database.EnsureCreatedAsync();

            var orderId = Guid.NewGuid();

            await dbContext.Orders.AddAsync(
                new Order
                {
                    Id = orderId,
                    ClientId = "client-001",
                    Symbol = "EURUSD",
                    Side = OrderSide.Buy,
                    OrderType = OrderType.Market,
                    Quantity = 100_000m,
                    Status = OrderStatus.PendingRisk,
                    CorrelationId = "risk-retry-test-001",
                    CreatedAt = DateTimeOffset.UtcNow
                });

            await dbContext.SaveChangesAsync();

            var riskDecisionId = Guid.NewGuid();

            var riskClient = CreateRiskClient(
                approved: true,
                reasonCode: "APPROVED",
                reason: "Order approved by risk checks.",
                riskDecisionId: riskDecisionId);

            var handler = CreateHandler(dbContext, riskClient);
            var messageContext = new TestableMessageHandlerContext();

            var commad = new SubmitOrder
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Quantity = 100_000m,
                CorrelationId = "risk-retry-test-001"
            };

            await handler.Handle(commad, messageContext);

            var orders = await dbContext.Orders.Where(x => x.Id == orderId).ToListAsync();

            orders.Should().ContainSingle();

            var order = orders.Single();

            order.Status.Should().Be(OrderStatus.Accepted);
            order.AcceptedAt.Should().NotBeNull();
            order.RejectedAt.Should().BeNull();

            messageContext.PublishedMessages.Should().ContainSingle();

            var published = messageContext.PublishedMessages.Single().Message
                .Should().BeOfType<OrderAccepted>().Subject;

            published.OrderId.Should().Be(orderId);
            published.RiskDecisionId.Should().Be(riskDecisionId.ToString());

            riskClient.Verify(x => x.CheckOrderRiskAsync(
                It.Is<SubmitOrder>(o => o.OrderId == orderId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WhenRiskApprovesLimitOrder_ShouldStartLimitOrder()
        {
            await using var connection = new SqliteConnection("Datasource=:memory:");

            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);

            await dbContext.Database.EnsureCreatedAsync();

            var riskDecisionId = Guid.NewGuid();

            var riskClient = CreateRiskClient(
                approved: true,
                reasonCode: "APPROVED",
                reason: "Order approved by risk checks.",
                riskDecisionId: riskDecisionId);

            var handler = CreateHandler(dbContext, riskClient);

            var messageContext = new TestableMessageHandlerContext();

            var orderId = Guid.NewGuid();

            var command = new SubmitOrder
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                Quantity = 100000m,
                LimitPrice = 1.0845m,
                CorrelationId = "submit-limit-order-test-001"
            };

            await handler.Handle(command, messageContext);

            var order = await dbContext.Orders.SingleAsync(
                    x => x.Id == orderId);

            order.Status.Should().Be(OrderStatus.Accepted);
            order.LimitPrice.Should().Be(1.0845m);

            messageContext.SentMessages
                .Should()
                .ContainSingle();

            var sent = messageContext.SentMessages
                .Single()
                .Message
                .Should()
                .BeOfType<StartLimitOrder>()
                .Subject;

            sent.OrderId.Should().Be(orderId);
            sent.ClientId.Should().Be("client-001");
            sent.Symbol.Should().Be("EURUSD");
            sent.Side.Should().Be(OrderSide.Buy);
            sent.Quantity.Should().Be(100000m);
            sent.LimitPrice.Should().Be(1.0845m);
            sent.CorrelationId.Should()
                .Be("submit-limit-order-test-001");

            messageContext.PublishedMessages
                .Should()
                .ContainSingle();

            var published = messageContext.PublishedMessages
                    .Single()
                    .Message
                    .Should()
                    .BeOfType<OrderAccepted>()
                    .Subject;

            published.OrderType.Should()
                .Be(OrderType.Limit);

            published.LimitPrice.Should()
                .Be(1.0845m);
        }
                
        private OrderDbContext CreateDbContext(SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<OrderDbContext>()
                .UseSqlite(connection)
                .Options;

            return new OrderDbContext(options);
        }

        private SubmitOrderHandler CreateHandler(
            OrderDbContext dbContext,
            Mock<IRiskClient> riskClient)
        {
            var unitOfWork = new EfUnitOfWork(
                dbContext,
                new OrderRepository(dbContext, NullLogger<OrderRepository>.Instance));

            return new SubmitOrderHandler(
                unitOfWork,
                riskClient.Object,
                NullLogger<SubmitOrderHandler>.Instance);
        }

        private static Mock<IRiskClient> CreateRiskClient(
            bool approved,
            string reasonCode,
            string reason,
            Guid riskDecisionId)
        {
            var riskClient = new Mock<IRiskClient>();

            riskClient.Setup(x => x.CheckOrderRiskAsync(
                    It.IsAny<SubmitOrder>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RiskCheckResult
                {
                    Approved = approved,
                    ReasonCode = reasonCode,
                    Reason = reason,
                    RiskDecisionId = riskDecisionId
                });

            return riskClient;
        }
    }
}
