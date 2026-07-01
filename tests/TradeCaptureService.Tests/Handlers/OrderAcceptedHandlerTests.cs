using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NServiceBus.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Domain;
using TradeCaptureService.Handlers;
using TradeCaptureService.Infrastructure.Persistence;
using TradeCaptureService.Infrastructure.Repositories;
using TradeCaptureService.Infrastructure.UnitOfWork;
using TradeCaptureService.Pricing;
using TradeCaptureService.Services;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;

namespace TradeCaptureService_tests.Handlers
{
    public class OrderAcceptedHandlerTests
    {
        [Fact]
        public async Task Handle_WhenBuyOrderAccepted_ShouldCreateTradeUsingAskPriceAndPublishTradeCaptured()
        {
            await using var connection = new SqliteConnection("Datasource=:memory:");
            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);
            await dbContext.Database.EnsureCreatedAsync();

            var pricingClient = CreatePricingClient(
                bid: 1.0849m,
                ask: 1.0851m,
                mid: 1.0850m);

            var handler = CreateHandler(dbContext, pricingClient);
            var messageContext = new TestableMessageHandlerContext();

            var orderId = Guid.NewGuid();

            var message = new OrderAccepted
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Quantity = 100000m,
                AcceptedAt = DateTimeOffset.UtcNow,
                RiskDecisionId = "risk-001",
                CorrelationId = "handler-test-buy"
            };

            await handler.Handle(message, messageContext);

            var trade = await dbContext.Trades.SingleAsync(x => x.OrderId == orderId);

            trade.ClientId.Should().Be("client-001");
            trade.Symbol.Should().Be("EURUSD");
            trade.Side.Should().Be(OrderSide.Buy);
            trade.OrderType.Should().Be(OrderType.Market);
            trade.Quantity.Should().Be(100000m);
            trade.Price.Should().Be(1.0851m);
            trade.Notional.Should().Be(108510m);
            trade.Status.Should().Be(TradeStatus.Captured);
            trade.CorrelationId.Should().Be("handler-test-buy");
            trade.CapturedAt.Should().NotBe(default);

            messageContext.PublishedMessages.Should().HaveCount(1);

            var published = messageContext.PublishedMessages.Single().Message
                .Should()
                .BeOfType<TradeCaptured>()
                .Subject;


            published.TradeId.Should().Be(trade.Id);
            published.OrderId.Should().Be(orderId);
            published.ClientId.Should().Be("client-001");
            published.Symbol.Should().Be("EURUSD");
            published.Side.Should().Be(OrderSide.Buy);
            published.Quantity.Should().Be(100000m);
            published.Price.Should().Be(1.0851m);
            published.Notional.Should().Be(108510m);
            published.Status.Should().Be(TradeStatus.Captured);
            published.CorrelationId.Should().Be("handler-test-buy");

            pricingClient.Verify(
                x => x.GetPriceAsync(
                    "EURUSD",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_WhenSellOrderAccepted_ShouldCreateTradeUsingBidPriceAndPublishTradeCaptured()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);
            await dbContext.Database.EnsureCreatedAsync();

            var pricingClient = CreatePricingClient(
                bid: 1.0849m,
                ask: 1.0851m,
                mid: 1.0850m);

            var handler = CreateHandler(dbContext, pricingClient);
            var messageContext = new TestableMessageHandlerContext();

            var orderId = Guid.NewGuid();

            var message = new OrderAccepted
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Sell,
                OrderType = OrderType.Market,
                Quantity = 100000m,
                AcceptedAt = DateTimeOffset.UtcNow,
                RiskDecisionId = "risk-002",
                CorrelationId = "handler-test-sell"
            };

            await handler.Handle(message, messageContext);

            var trade = await dbContext.Trades.SingleAsync(x => x.OrderId == orderId);

            trade.Side.Should().Be(OrderSide.Sell);
            trade.Price.Should().Be(1.0849m);
            trade.Notional.Should().Be(108490m);
            trade.Status.Should().Be(TradeStatus.Captured);
            trade.CorrelationId.Should().Be("handler-test-sell");

            messageContext.PublishedMessages.Should().HaveCount(1);

            var published = messageContext.PublishedMessages.Single().Message
                .Should()
                .BeOfType<TradeCaptured>()
                .Subject;

            published.Side.Should().Be(OrderSide.Sell);
            published.Price.Should().Be(1.0849m);
            published.Notional.Should().Be(108490m);
            published.CorrelationId.Should().Be("handler-test-sell");

            pricingClient.Verify(x => x.GetPriceAsync(
                    "EURUSD",
                    "handler-test-sell",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_WhenTradeAlreadyExistsForOrder_ShouldSkipDuplicateAndNotPublishTradeCaptured()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);
            await dbContext.Database.EnsureCreatedAsync();

            var orderId = Guid.NewGuid();

            dbContext.Trades.Add(new Trade
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Quantity = 100000m,
                Price = 1.0851m,
                Notional = 108510m,
                Status = TradeStatus.Captured,
                CapturedAt = DateTimeOffset.UtcNow,
                CorrelationId = "existing-trade"
            });

            await dbContext.SaveChangesAsync();

            var pricingClient = CreatePricingClient(
                bid: 1.0849m,
                ask: 1.0851m,
                mid: 1.0850m);

            var handler = CreateHandler(dbContext, pricingClient);
            var messageContext = new TestableMessageHandlerContext();

            var message = new OrderAccepted
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Quantity = 100000m,
                AcceptedAt = DateTimeOffset.UtcNow,
                RiskDecisionId = "risk-duplicate",
                CorrelationId = "handler-test-duplicate"
            };

            await handler.Handle(message, messageContext);

            var trades = await dbContext.Trades
                .Where(x => x.OrderId == orderId)
                .ToListAsync();

            trades.Should().HaveCount(1);
            trades.Single().CorrelationId.Should().Be("existing-trade");

            messageContext.PublishedMessages.Should().BeEmpty();

            pricingClient.Verify(x => x.GetPriceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private TradeDbContext CreateDbContext(SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<TradeDbContext>()
                .UseSqlite(connection)
                .Options;

            return new TradeDbContext(options);
        }

        private OrderAcceptedHandler CreateHandler(
            TradeDbContext dbContext, 
            Mock<IPricingClient> pricingClient)
        {
            var unitOfWork = new EfUnitOfWork(
                dbContext,
                new TradeRepository(dbContext, NullLogger<TradeRepository>.Instance));

            return new OrderAcceptedHandler(
                unitOfWork,
                pricingClient.Object,
                new ExecutionPriceCalculator(),
                NullLogger<OrderAcceptedHandler>.Instance);
        }

        private static Mock<IPricingClient> CreatePricingClient(
            decimal bid,
            decimal ask,
            decimal mid)
        {
            var pricingClient = new Mock<IPricingClient>();

            pricingClient.Setup(x => x.GetPriceAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync((string symbol, string? _, CancellationToken _) => new PriceQuote
                {
                    Symbol = symbol,
                    Bid = bid,
                    Ask = ask,
                    Mid = mid
                });

            return pricingClient;
        }
    }
}
