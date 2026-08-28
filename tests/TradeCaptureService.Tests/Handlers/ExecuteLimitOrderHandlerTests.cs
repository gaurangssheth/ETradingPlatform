using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NServiceBus.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Application;
using TradeCaptureService.Calculations;
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

namespace TradeCaptureService.Tests.Handlers
{
    public class ExecuteLimitOrderHandlerTests
    {
        [Fact]
        public async Task Handle_WhenExecuteLimitOrderReceived_ShouldCaptureTradeAtExecutionPrice()
        {
            await using var connection = new SqliteConnection("Datasource=:memory:");

            await connection.OpenAsync();

            await using var dbContext =
                CreateDbContext(connection);

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

            var handler = CreateHandler(dbContext, referenceDataClient);
            var messageContext = new TestableMessageHandlerContext();

            var orderId = Guid.NewGuid();

            var message =
                new ExecuteLimitOrder
                {
                    OrderId = orderId,
                    ClientId = "client-001",
                    Symbol = "EURUSD",
                    Side = OrderSide.Buy,
                    Quantity = 100000m,
                    LimitPrice = 1.0850m,
                    ExecutionPrice = 1.0848m,
                    RiskDecisionId = "risk-001",
                    ExecutedAt = DateTimeOffset.UtcNow,
                    CorrelationId = "limit-execution-test-001"
                };

            await handler.Handle(message, messageContext);

            var trade = await dbContext.Trades.SingleAsync(
                    x => x.OrderId == orderId);

            trade.OrderType.Should().Be(OrderType.Limit);
            trade.Price.Should().Be(1.0848m);
            trade.Quantity.Should().Be(100000m);

            var published = messageContext.PublishedMessages
                    .Single()
                    .Message
                    .Should()
                    .BeOfType<TradeCaptured>()
                    .Subject;

            published.OrderId.Should().Be(orderId);
            published.Price.Should().Be(1.0848m);
        }

        private TradeDbContext CreateDbContext(SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<TradeDbContext>()
                .UseSqlite(connection)
                .Options;

            return new SqliteTradeDbContext(options);
        }

        private ExecuteLimitOrderHandler CreateHandler(
            TradeDbContext dbContext,
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

            return new ExecuteLimitOrderHandler(
                tradeCaptureProcessor,
                NullLogger<ExecuteLimitOrderHandler>.Instance);
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
