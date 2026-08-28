using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NServiceBus.Testing;
using NServiceBus.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Application;
using TradeCaptureService.Calculations;
using TradeCaptureService.Domain;
using TradeCaptureService.Handlers;
using TradeCaptureService.Infrastructure.Persistence;
using TradeCaptureService.Infrastructure.Repositories;
using TradeCaptureService.Infrastructure.UnitOfWork;
using TradeCaptureService.Pricing;
using TradeCaptureService.ReferenceData;
using TradeCaptureService.Services;
using TradeCaptureService.Tests.Infrastructure.Persistence;
using TradingApp.Contracts.Commands;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;
using TradingApp.SharedKernel;
using static PricingService.Grpc.Pricing;
using static ReferenceDataService.Grpc.ReferenceData;

namespace TradeCaptureService.Tests.Handlers
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

            var instrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            var instrument = new InstrumentReferenceData
            {
                InstrumentId = instrumentId,
                Symbol = "EURUSD",
                AssetClass = AssetClass.Fx,
                IsTradable = true
            };

            var details = new FxInstrumentReferenceDetails(instrument.InstrumentId,
                "EUR",
                "USD",
                0.0001m);

            var referenceDataClient = CreateReferenceDataClient(instrument, details);

            var pricingClient = CreatePricingClient(
                bid: 1.0849m,
                ask: 1.0851m,
                mid: 1.0850m);

            var handler = CreateHandler(dbContext, pricingClient, referenceDataClient);
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
            published.InstrumentId.Should().Be(trade.InstrumentId);
            published.AssetClass.Should().Be(TradingApp.SharedKernel.AssetClass.Fx);
            published.NotionalCurrency.Should().Be("USD");

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

            var instrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            var instrument = new InstrumentReferenceData
            {
                InstrumentId = instrumentId,
                Symbol = "EURUSD",
                AssetClass = AssetClass.Fx,
                IsTradable = true
            };

            var details = new FxInstrumentReferenceDetails(instrument.InstrumentId,
                "EUR",
                "USD",
                0.0001m);

            var referenceDataClient = CreateReferenceDataClient(instrument, details);

            var pricingClient = CreatePricingClient(
                bid: 1.0849m,
                ask: 1.0851m,
                mid: 1.0850m);

            var handler = CreateHandler(dbContext, pricingClient, referenceDataClient);
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
                InstrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ClientId = "client-001",
                Symbol = "EURUSD",
                AssetClass = AssetClass.Fx,
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Quantity = 100000m,
                Price = 1.0851m,
                Notional = 108510m,
                NotionalCurrency = new CurrencyCode("USD"),
                Status = TradeStatus.Captured,
                CapturedAt = DateTimeOffset.UtcNow,
                CorrelationId = "existing-trade"
            });

            await dbContext.SaveChangesAsync();

            var pricingClient = CreatePricingClient(
                bid: 1.0849m,
                ask: 1.0851m,
                mid: 1.0850m);

            var instrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            var instrument = new InstrumentReferenceData
            {
                InstrumentId = instrumentId,
                Symbol = "EURUSD",
                AssetClass = AssetClass.Fx,
                IsTradable = true
            };

            var details = new FxInstrumentReferenceDetails(instrument.InstrumentId,
                "EUR",
                "USD",
                0.0001m);

            var referenceDataClient = CreateReferenceDataClient(instrument, details);

            var handler = CreateHandler(dbContext, pricingClient, referenceDataClient);
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

        [Fact]
        public async Task Handle_WhenEquityBuyOrderAccepted_ShouldUseEquityNotionalCalculator()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");

            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);
            await dbContext.Database.EnsureCreatedAsync();

            var instrumentId = Guid.Parse("22222222-2222-2222-2222-222222222222");

            var instrument = new InstrumentReferenceData
            {
                InstrumentId = instrumentId,
                Symbol = "AAPL",
                AssetClass = AssetClass.Equity,
                IsTradable = true
            };

            var details = new EquityInstrumentReferenceDetails(
                instrumentId,
                "NASDAQ",
                "USD");

            var referenceDataClient = CreateReferenceDataClient(instrument, details);

            var pricingClient = CreatePricingClient(
                bid: 210.00m,
                ask: 210.50m,
                mid: 210.25m);

            var handler = CreateHandler(dbContext, pricingClient, referenceDataClient);

            var messageContext = new TestableMessageHandlerContext();

            var orderId = Guid.NewGuid();

            var message = new OrderAccepted
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "AAPL",
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Quantity = 100m,
                AcceptedAt = DateTimeOffset.UtcNow,
                RiskDecisionId = "risk-equity-001",
                CorrelationId = "handler-test-equity"
            };

            await handler.Handle(message, messageContext);

            var trade = await dbContext.Trades
                .SingleAsync(x => x.OrderId == orderId);

            trade.InstrumentId.Should().Be(instrumentId);
            trade.Symbol.Should().Be("AAPL");
            trade.AssetClass.Should().Be(AssetClass.Equity);
            trade.Price.Should().Be(210.50m);
            trade.Quantity.Should().Be(100m);
            trade.Notional.Should().Be(21_050m);

            trade.NotionalCurrency
                .Should()
                .Be(new CurrencyCode("USD"));

            messageContext.PublishedMessages.Should().HaveCount(1);
            var published = messageContext.PublishedMessages.Single().Message
                .Should()
                .BeOfType<TradeCaptured>()
                .Subject;

            published.AssetClass.Should().Be(TradingApp.SharedKernel.AssetClass.Equity);
            published.NotionalCurrency.Should().Be("USD");

            referenceDataClient.Verify(
                x => x.GetInstrumentAsync(
                    "AAPL",
                    "handler-test-equity",
                    It.IsAny<CancellationToken>()),
                Times.Once);

            pricingClient.Verify(
                x => x.GetPriceAsync(
                    "AAPL",
                    "handler-test-equity",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_WhenFixedIncomeBuyOrderAccepted_ShouldUseBondNotionalCalculator()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");

            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);
            await dbContext.Database.EnsureCreatedAsync();

            var instrumentId = Guid.Parse("33333333-3333-3333-3333-333333333333");

            var instrument = new InstrumentReferenceData
            {
                InstrumentId = instrumentId,
                Symbol = "GB00TEST1234",
                AssetClass = AssetClass.FixedIncome,
                IsTradable = true
            };

            var details = new BondInstrumentReferenceDetails(
                instrumentId,
                isin: "GB00TEST1234",
                issuer: "UK Government",
                denominationCurrency: "GBP",
                couponRate: 4.25m,
                maturityDate: new DateOnly(2035, 6, 30),
                parValue: 100m,
                dayCountConvention: DayCountConvention.ActualActual);

            var referenceDataClient = CreateReferenceDataClient(instrument, details);

            var pricingClient = CreatePricingClient(
                bid: 98.40m,
                ask: 98.50m,
                mid: 98.45m);

            var handler = CreateHandler(dbContext, pricingClient, referenceDataClient);

            var messageContext = new TestableMessageHandlerContext();

            var orderId = Guid.NewGuid();

            var message = new OrderAccepted
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "GB00TEST1234",
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Quantity = 1_000_000m,
                AcceptedAt = DateTimeOffset.UtcNow,
                RiskDecisionId = "risk-equity-001",
                CorrelationId = "handler-test-bond"
            };

            await handler.Handle(message, messageContext);

            var trade = await dbContext.Trades
                .SingleAsync(x => x.OrderId == orderId);

            trade.InstrumentId.Should().Be(instrumentId);
            trade.Symbol.Should().Be("GB00TEST1234");
            trade.AssetClass.Should().Be(AssetClass.FixedIncome);
            trade.Price.Should().Be(98.50m);
            trade.Quantity.Should().Be(1_000_000m);
            trade.Notional.Should().Be(985_000m);

            trade.NotionalCurrency
            .Should()
            .Be(new CurrencyCode("GBP"));

            messageContext.PublishedMessages.Should().HaveCount(1);
            var published = messageContext.PublishedMessages.Single().Message
                .Should()
                .BeOfType<TradeCaptured>()
                .Subject;

            published.AssetClass.Should().Be(TradingApp.SharedKernel.AssetClass.FixedIncome);
            published.NotionalCurrency.Should().Be("GBP");

            referenceDataClient.Verify(
                x => x.GetInstrumentAsync(
                    "GB00TEST1234",
                    "handler-test-bond",
                    It.IsAny<CancellationToken>()),
                Times.Once);

            pricingClient.Verify(
                x => x.GetPriceAsync(
                    "GB00TEST1234",
                    "handler-test-bond",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_WhenOrderIsLimit_ShouldNotCaptureTrade()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");

            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);
            await dbContext.Database.EnsureCreatedAsync();

            var orderId = Guid.NewGuid();

            var message = new OrderAccepted
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                Quantity = 100000m,
                OrderType = OrderType.Limit,
                LimitPrice = 1.0845m,
                AcceptedAt = DateTimeOffset.UtcNow,
                RiskDecisionId = Guid.NewGuid().ToString(),
                CorrelationId = "limit-order-handler-test-001"
            };

            var instrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            var instrument = new InstrumentReferenceData
            {
                InstrumentId = instrumentId,
                Symbol = "EURUSD",
                AssetClass = AssetClass.Fx,
                IsTradable = true
            };

            var details = new FxInstrumentReferenceDetails(instrument.InstrumentId,
                "EUR",
                "USD",
                0.0001m);

            var pricingClient = CreatePricingClient(
                bid: 1.0849m,
                ask: 1.0851m,
                mid: 1.0850m);

            var referenceDataClient = CreateReferenceDataClient(instrument, details);

            var handler = CreateHandler(dbContext, pricingClient, referenceDataClient);

            var messageContext = new TestableMessageHandlerContext();

            await handler.Handle(message, messageContext);

            pricingClient.Verify(
                x => x.GetPriceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            var trade =
                await dbContext.Trades.SingleOrDefaultAsync(
                    x => x.OrderId == orderId);

            trade.Should().BeNull();
        }

        private TradeDbContext CreateDbContext(SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<TradeDbContext>()
                .UseSqlite(connection)
                .Options;

            return new SqliteTradeDbContext(options);
        }

        private OrderAcceptedHandler CreateHandler(
            TradeDbContext dbContext, 
            Mock<IPricingClient> pricingClient,
            Mock<IReferenceDataClient> referenceDataClient)
        {
            var unitOfWork = new EfUnitOfWork(
                dbContext,
                new TradeRepository(dbContext, NullLogger<TradeRepository>.Instance));

            var tradeCaptureProcessor =
                new TradeCaptureProcessor(
                unitOfWork,
                referenceDataClient.Object,
                CreateResolver(),
                NullLogger<TradeCaptureProcessor>.Instance);

            return new OrderAcceptedHandler(
                pricingClient.Object,
                new ExecutionPriceCalculator(),
                tradeCaptureProcessor,
                NullLogger<OrderAcceptedHandler>.Instance);
        }

        private static NotionalCalculatorResolver CreateResolver()
        {
            return new NotionalCalculatorResolver(
                new INotionalCalculator[]
                {
                new FxNotionalCalculator(),
                new EquityNotionalCalculator(),
                new BondNotionalCalculator()
                });
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

        private static Mock<IReferenceDataClient> CreateReferenceDataClient(
            InstrumentReferenceData instrumentReferenceData, IInstrumentReferenceDetails instrumentReferenceDetails)
        {
            var referenceDataClient = new Mock<IReferenceDataClient>();

            referenceDataClient.Setup(x => x.GetInstrumentAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync((
                string symbol,
                string? _,
                CancellationToken _) =>
                {
                    return new InstrumentReferenceDefinition
                    (
                        instrumentReferenceData,
                        instrumentReferenceDetails
                    );
                });

            return referenceDataClient;
        }
    }
}
