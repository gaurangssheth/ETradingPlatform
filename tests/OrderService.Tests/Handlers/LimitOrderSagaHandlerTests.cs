using FluentAssertions;
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
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;

namespace OrderService.Tests.Handlers
{
    public class LimitOrderSagaHandlerTests
    {
        [Fact]
        public async Task Handle_StartLimitOrder_ShouldPopulateSagaData()
        {
            await using var connection = new SqliteConnection("Datasource=:memory:");

            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);

            await dbContext.Database.EnsureCreatedAsync();

            var pricingClient = new Mock<IPricingClient>();

            var sagaHandler = CreateHandler(dbContext, pricingClient);

            var sagaData = new LimitOrderSagaData();

            sagaHandler.Data = sagaData;

            var context = new TestableMessageHandlerContext();

            var orderId = Guid.NewGuid();

            var order = new Order
            {
                Id = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                Quantity = 100000m,
                LimitPrice = 1.0845m,
                Status = OrderStatus.Working,
                CorrelationId = "limit-saga-test-001",
                CreatedAt = DateTimeOffset.UtcNow,
                AcceptedAt = DateTimeOffset.UtcNow
            };

            dbContext.Orders.Add(order);

            await dbContext.SaveChangesAsync();

            var message = new StartLimitOrder
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                Quantity = 100000m,
                LimitPrice = 1.0845m,
                RiskDecisionId = "risk-decision-001",
                CorrelationId = "limit-saga-test-001"
            };

            await sagaHandler.Handle(message, context);

            sagaData.OrderId.Should().Be(orderId);
            sagaData.ClientId.Should().Be("client-001");
            sagaData.Symbol.Should().Be("EURUSD");
            sagaData.Side.Should().Be(OrderSide.Buy);
            sagaData.Quantity.Should().Be(100000m);
            sagaData.LimitPrice.Should().Be(1.0845m);
            sagaData.Status.Should().Be(LimitOrderSagaStatus.Working);
            sagaData.RiskDecisionId.Should().Be("risk-decision-001");
            sagaData.CorrelationId.Should().Be("limit-saga-test-001");

            order.Status.Should().Be(OrderStatus.Working);
        }


        [Theory]
        [InlineData(OrderSide.Buy, 1.0848, 1.0850, 1.0850, LimitOrderSagaStatus.Triggered, 0, 1.0850)]
        [InlineData(OrderSide.Buy, 1.0848, 1.0850, 1.0849, LimitOrderSagaStatus.Working, 1, null)]
        [InlineData(OrderSide.Sell, 1.0848, 1.0850, 1.0848, LimitOrderSagaStatus.Triggered, 0, 1.0848)]
        [InlineData(OrderSide.Sell, 1.0848, 1.0850, 1.0849, LimitOrderSagaStatus.Working, 1, null)]
        public async Task Timeout_ShouldUpdateStatusAndRescheduleAsExpected(
            OrderSide side,
            double bid,
            double ask,
            double limitPrice,
            LimitOrderSagaStatus expectedStatus,
            int expectedTimeoutCount,
            double? expectedExecutionPrice)
        {
            await using var connection = new SqliteConnection("Datasource=:memory:");

            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);

            await dbContext.Database.EnsureCreatedAsync();

            var pricingClient = new Mock<IPricingClient>();

            pricingClient
                .Setup(x => x.GetPriceAsync(
                    "EURUSD",
                    "limit-order-saga-test-001",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PriceQuote
                (
                    Symbol: "EURUSD",
                    Bid: (decimal)bid,
                    Ask: (decimal)ask,
                    Mid: ((decimal)bid + (decimal)ask) / 2
                ));

            var handler = CreateHandler(dbContext, pricingClient);

            var orderId = Guid.NewGuid();

            var order = new Order
            {
                Id = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                Quantity = 100000m,
                LimitPrice = 1.0845m,
                Status = OrderStatus.Working,
                CorrelationId = "limit-saga-test-001",
                CreatedAt = DateTimeOffset.UtcNow,
                AcceptedAt = DateTimeOffset.UtcNow
            };

            dbContext.Orders.Add(order);

            await dbContext.SaveChangesAsync();

            var sagaData =
                new LimitOrderSagaData
                {
                    OrderId = orderId,
                    ClientId = "client-001",
                    Symbol = "EURUSD",
                    Side = side,
                    Quantity = 100000m,
                    LimitPrice = (decimal)limitPrice,
                    Status = LimitOrderSagaStatus.Working,
                    RiskDecisionId = "risk-decision-001",
                    CorrelationId = "limit-order-saga-test-001"
                };

            handler.Data = sagaData;

            var context = new TestableMessageHandlerContext();

            await handler.Timeout(new CheckOrderLimitPrice(), context);

            sagaData.Status.Should().Be(expectedStatus);

            context.TimeoutMessages.Should()
                .HaveCount(expectedTimeoutCount);

            if (expectedExecutionPrice.HasValue)
            {
                var sent = context.SentMessages
                    .Single()
                    .Message
                    .Should()
                    .BeOfType<ExecuteLimitOrder>()
                    .Subject;

                sent.OrderId.Should().Be(sagaData.OrderId);
                sent.Symbol.Should().Be("EURUSD");
                sent.Side.Should().Be(side);
                sent.Quantity.Should().Be(100000m);
                sent.LimitPrice.Should().Be((decimal)limitPrice);
                sent.ExecutionPrice.Should().Be((decimal)expectedExecutionPrice.Value);
                sent.ExecutedAt.Should().NotBe(default);
                sent.RiskDecisionId.Should().Be(sagaData.RiskDecisionId);
                sent.CorrelationId.Should().Be("limit-order-saga-test-001");
            }
            else
            {
                context.SentMessages
                .Select(x => x.Message)
                .OfType<ExecuteLimitOrder>()
                .Should()
                .BeEmpty();
            }

            sagaData.Status.Should().Be(expectedStatus);

            var expectedOrderStatus = expectedStatus == LimitOrderSagaStatus.Triggered
                    ? OrderStatus.Triggered
                    : OrderStatus.Working;

            order.Status.Should().Be(expectedOrderStatus);
        }

        [Fact]
        public async Task Handle_TradeCaptured_ShouldMarkSagaAsFilledAndComplete()
        {
            await using var connection = new SqliteConnection("Datasource=:memory:");

            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);

            await dbContext.Database.EnsureCreatedAsync();

            var pricingClient = new Mock<IPricingClient>();

            var handler = CreateHandler(dbContext, pricingClient);

            var orderId = Guid.NewGuid();

            var order = new Order
            {
                Id = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                Quantity = 100000m,
                LimitPrice = 1.0845m,
                Status = OrderStatus.Triggered,
                CorrelationId = "limit-saga-test-001",
                CreatedAt = DateTimeOffset.UtcNow,
                AcceptedAt = DateTimeOffset.UtcNow
            };

            dbContext.Orders.Add(order);

            await dbContext.SaveChangesAsync();

            var sagaData = new LimitOrderSagaData
                {
                    OrderId = orderId,
                    ClientId = "client-001",
                    Symbol = "EURUSD",
                    Side = OrderSide.Buy,
                    Quantity = 100000m,
                    LimitPrice = 1.0845m,
                    Status = LimitOrderSagaStatus.Triggered,
                    RiskDecisionId = "risk-001",
                    CorrelationId = "limit-order-complete-test-001"
                };

            handler.Data = sagaData;

            var context = new TestableMessageHandlerContext();

            var message = new TradeCaptured
                {
                    TradeId = Guid.NewGuid(),
                    OrderId = orderId,
                    Symbol = "EURUSD",
                    CorrelationId = "limit-order-complete-test-001"
                };

            await handler.Handle(message, context);

            sagaData.Status.Should().Be(LimitOrderSagaStatus.Filled);

            order.Status.Should().Be(OrderStatus.Filled);
        }

        private LimitOrderSagaHandler CreateHandler(
            OrderDbContext dbContext,
            Mock<IPricingClient> pricingClient)
        {
            var unitOfWork = new EfUnitOfWork(
                dbContext,
                new OrderRepository(dbContext, NullLogger<OrderRepository>.Instance));

            return new LimitOrderSagaHandler(
                unitOfWork,
                pricingClient.Object,
                new LimitOrderExecutionEvaluator(),
                NullLogger<LimitOrderSagaHandler>.Instance);
        }

        private OrderDbContext CreateDbContext(SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<OrderDbContext>()
                .UseSqlite(connection)
                .Options;

            return new OrderDbContext(options);
        }
    }
}
