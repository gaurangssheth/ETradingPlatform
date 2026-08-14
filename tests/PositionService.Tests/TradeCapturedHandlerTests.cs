using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.Testing;
using PositionService.Application.PositionAccounting;
using PositionService.Domain;
using PositionService.Handlers;
using PositionService.Infrastructure.Persistence;
using PositionService.Infrastructure.Repositories;
using PositionService.Infrastructure.UnitOfWork;
using PositionService.Tests.Infrastructure.Persistence;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;
using TradingApp.SharedKernel;

namespace PositionService.Tests;

public class TradeCapturedHandlerTests
{
    [Fact]
    public async Task Handle_WhenFirstBuyTrade_ShouldCreatePositionMovementProcessedTradeAndPublishPositionUpdated()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();

        var handler = CreateHandler(dbContext);
        var messageContext = new TestableMessageHandlerContext();

        var tradeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var instrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var message = new TradeCaptured
        {
            TradeId = tradeId,
            OrderId = orderId,
            ClientId = "client-001",
            InstrumentId = instrumentId,
            Symbol = "EURUSD",
            AssetClass = AssetClass.Fx,
            NotionalCurrency = "USD",
            Side = OrderSide.Buy,
            Quantity = 100m,
            Price = 1.0800m,
            CapturedAt = DateTimeOffset.UtcNow,
            CorrelationId = "handler-test-001"
        };

        await handler.Handle(message, messageContext);

        var position = await dbContext.Positions.SingleAsync(
            x => x.ClientId == message.ClientId && x.InstrumentId == message.InstrumentId);

        position.ClientId.Should().Be("client-001");
        position.InstrumentId.Should().Be(instrumentId);
        position.Symbol.Should().Be("EURUSD");
        position.AssetClass.Should().Be(AssetClass.Fx);
        position.PnlCurrency.Should().Be(new CurrencyCode("USD"));
        position.NetQuantity.Should().Be(100m);
        position.AveragePrice.Should().Be(1.0800m);
        position.RealisedPnl.Should().Be(0m);
        position.UnrealisedPnl.Should().Be(0m);
        position.CorrelationId.Should().Be("handler-test-001");

        var movement = await dbContext.PositionMovements.SingleAsync(x => x.TradeId == tradeId);

        movement.PositionId.Should().Be(position.Id);
        movement.TradeId.Should().Be(tradeId);
        movement.OrderId.Should().Be(orderId);
        movement.InstrumentId.Should().Be(instrumentId);
        movement.AssetClass.Should().Be(AssetClass.Fx);
        movement.PnlCurrency.Should().Be(new CurrencyCode("USD"));
        movement.Side.Should().Be(OrderSide.Buy);
        movement.Quantity.Should().Be(100m);
        movement.SignedQuantity.Should().Be(100m);
        movement.Price.Should().Be(1.0800m);
        movement.PreviousNetQuantity.Should().Be(0m);
        movement.PreviousAveragePrice.Should().Be(0m);
        movement.NewNetQuantity.Should().Be(100m);
        movement.NewAveragePrice.Should().Be(1.0800m);
        movement.PreviousRealisedPnl.Should().Be(0m);
        movement.RealisedPnlChange.Should().Be(0m);
        movement.NewRealisedPnl.Should().Be(0m);
        movement.CorrelationId.Should().Be("handler-test-001");

        var processedTrade = await dbContext.ProcessedTrades.SingleAsync(x => x.TradeId == tradeId);

        processedTrade.TradeId.Should().Be(tradeId);

        messageContext.PublishedMessages.Should().HaveCount(1);

        var published = messageContext.PublishedMessages.Single().Message
            .Should()
            .BeOfType<PositionUpdated>()
            .Subject;

        published.PositionId.Should().Be(position.Id);
        published.ClientId.Should().Be("client-001");
        published.InstrumentId.Should().Be(instrumentId);
        published.Symbol.Should().Be("EURUSD");
        published.AssetClass.Should().Be(AssetClass.Fx);
        published.PnlCurrency.Should().Be("USD");
        published.NetQuantity.Should().Be(100m);
        published.AveragePrice.Should().Be(1.0800m);
        published.RealisedPnl.Should().Be(0m);
        published.UnrealisedPnl.Should().Be(0m);
        published.CorrelationId.Should().Be("handler-test-001");
    }

    [Fact]
    public async Task Handle_WhenSellReducesExistingLong_ShouldKeepAveragePriceAndRealiseProfit()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();

        var instrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var existingPosition = new Position
        {
            Id = Guid.NewGuid(),
            ClientId = "client-001",
            InstrumentId = instrumentId,
            Symbol = "EURUSD",
            AssetClass = AssetClass.Fx,
            PnlCurrency = new CurrencyCode("USD"),
            NetQuantity = 100m,
            AveragePrice = 1.0800m,
            RealisedPnl = 0m,
            UnrealisedPnl = 0m,
            CorrelationId = "seed-position",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Positions.Add(existingPosition);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var messageContext = new TestableMessageHandlerContext();

        var tradeId = Guid.NewGuid();

        var message = new TradeCaptured
        {
            TradeId = tradeId,
            OrderId = Guid.NewGuid(),
            ClientId = "client-001",
            InstrumentId = instrumentId,
            Symbol = "EURUSD",
            AssetClass = AssetClass.Fx,
            NotionalCurrency = "USD",
            Side = OrderSide.Sell,
            Quantity = 40m,
            Price = 1.0900m,
            CapturedAt = DateTimeOffset.UtcNow,
            CorrelationId = "handler-test-002"
        };

        await handler.Handle(message, messageContext);

        var position = await dbContext.Positions.SingleAsync(
            x => x.ClientId == message.ClientId && x.InstrumentId == message.InstrumentId);

        position.NetQuantity.Should().Be(60m);
        position.AveragePrice.Should().Be(1.0800m);
        position.RealisedPnl.Should().Be(0.4000m);
        position.UnrealisedPnl.Should().Be(0m);
        position.CorrelationId.Should().Be("handler-test-002");

        var movement = await dbContext.PositionMovements.SingleAsync(x => x.TradeId == tradeId);

        movement.Side.Should().Be(OrderSide.Sell);
        movement.Quantity.Should().Be(40m);
        movement.SignedQuantity.Should().Be(-40m);
        movement.Price.Should().Be(1.0900m);
        movement.PreviousNetQuantity.Should().Be(100m);
        movement.PreviousAveragePrice.Should().Be(1.0800m);
        movement.NewNetQuantity.Should().Be(60m);
        movement.NewAveragePrice.Should().Be(1.0800m);
        movement.PreviousRealisedPnl.Should().Be(0m);
        movement.RealisedPnlChange.Should().Be(0.4000m);
        movement.NewRealisedPnl.Should().Be(0.4000m);
        movement.InstrumentId.Should().Be(instrumentId);
        movement.AssetClass.Should().Be(AssetClass.Fx);
        movement.PnlCurrency.Should().Be(new CurrencyCode("USD"));

        await dbContext.ProcessedTrades
            .Where(x => x.TradeId == tradeId)
            .CountAsync()
            .ContinueWith(t => t.Result.Should().Be(1));

        messageContext.PublishedMessages.Should().HaveCount(1);

        var published = messageContext.PublishedMessages.Single().Message
            .Should()
            .BeOfType<PositionUpdated>()
            .Subject;

        published.InstrumentId.Should().Be(instrumentId);
        published.Symbol.Should().Be("EURUSD");
        published.AssetClass.Should().Be(AssetClass.Fx);
        published.PnlCurrency.Should().Be("USD");
        published.NetQuantity.Should().Be(60m);
        published.AveragePrice.Should().Be(1.0800m);
        published.RealisedPnl.Should().Be(0.4000m);
    }

    [Fact]
    public async Task Handle_WhenBondSellClosesExistingLong_ShouldUseBondRealisedPnlCalculator()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();

        var instrumentId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var existingPosition = new Position
        {
            Id = Guid.NewGuid(),
            ClientId = "client-001",
            InstrumentId = instrumentId,
            Symbol = "GB00TEST1234",
            AssetClass = AssetClass.FixedIncome,

            NetQuantity = 1_000_000m,
            AveragePrice = 98.50m,

            PnlCurrency = new CurrencyCode("GBP"),
            RealisedPnl = 0m,
            UnrealisedPnl = 0m,

            CorrelationId = "seed-bond-position",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Positions.Add(existingPosition);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var messageContext = new TestableMessageHandlerContext();

        var tradeId = Guid.NewGuid();

        var message = new TradeCaptured
        {
            TradeId = tradeId,
            OrderId = Guid.NewGuid(),
            ClientId = "client-001",

            InstrumentId = instrumentId,
            Symbol = "GB00TEST1234",
            AssetClass = AssetClass.FixedIncome,
            NotionalCurrency = "GBP",

            Side = OrderSide.Sell,
            Quantity = 1_000_000m,
            Price = 99.50m,

            CapturedAt = DateTimeOffset.UtcNow,
            CorrelationId = "handler-bond-001"
        };

        await handler.Handle(message, messageContext);

        var position = await dbContext.Positions.SingleAsync(
            x => x.ClientId == message.ClientId && x.InstrumentId == message.InstrumentId);

        position.NetQuantity.Should().Be(0m);
        position.AveragePrice.Should().Be(0m);
        position.RealisedPnl.Should().Be(10_000m);
        position.PnlCurrency.Should().Be(new CurrencyCode("GBP"));

        var movement = await dbContext.PositionMovements
            .SingleAsync(x => x.TradeId == tradeId);

        movement.PreviousNetQuantity.Should().Be(1_000_000m);
        movement.NewNetQuantity.Should().Be(0m);

        movement.PreviousAveragePrice.Should().Be(98.50m);
        movement.NewAveragePrice.Should().Be(0m);

        movement.PreviousRealisedPnl.Should().Be(0m);
        movement.RealisedPnlChange.Should().Be(10_000m);
        movement.NewRealisedPnl.Should().Be(10_000m);

        movement.AssetClass.Should().Be(AssetClass.FixedIncome);
        movement.PnlCurrency.Should().Be(new CurrencyCode("GBP"));

        var published = messageContext.PublishedMessages.Single().Message
        .Should()
        .BeOfType<PositionUpdated>()
        .Subject;

        published.InstrumentId.Should().Be(instrumentId);
        published.AssetClass.Should().Be(AssetClass.FixedIncome);
        published.NetQuantity.Should().Be(0m);
        published.AveragePrice.Should().Be(0m);
        published.RealisedPnl.Should().Be(10_000m);
        published.PnlCurrency.Should().Be("GBP");
    }

    [Fact]
    public async Task Handle_WhenBondSellFlipsLongToShort_ShouldRealisePnlOnClosedNominalAndOpenShortAtTradePrice()
    {
        await using var connection =
            new SqliteConnection("DataSource=:memory:");

        await connection.OpenAsync();

        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();

        var instrumentId =
            Guid.Parse("33333333-3333-3333-3333-333333333333");

        var existingPosition = new Position
        {
            Id = Guid.NewGuid(),
            ClientId = "client-001",
            InstrumentId = instrumentId,
            Symbol = "GB00TEST1234",
            AssetClass = AssetClass.FixedIncome,
            PnlCurrency = new CurrencyCode("GBP"),

            NetQuantity = 1_000_000m,
            AveragePrice = 98.50m,
            RealisedPnl = 0m,
            UnrealisedPnl = 0m,

            CorrelationId = "seed-bond-position",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Positions.Add(existingPosition);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var messageContext = new TestableMessageHandlerContext();

        var tradeId = Guid.NewGuid();

        var message = new TradeCaptured
        {
            TradeId = tradeId,
            OrderId = Guid.NewGuid(),
            ClientId = "client-001",

            InstrumentId = instrumentId,
            Symbol = "GB00TEST1234",
            AssetClass = AssetClass.FixedIncome,
            NotionalCurrency = "GBP",

            Side = OrderSide.Sell,
            Quantity = 1_500_000m,
            Price = 99.50m,

            CapturedAt = DateTimeOffset.UtcNow,
            CorrelationId = "handler-bond-flip-001"
        };

        await handler.Handle(message, messageContext);

        var position = await dbContext.Positions.SingleAsync(
            x => x.ClientId == message.ClientId &&
                 x.InstrumentId == message.InstrumentId);

        position.NetQuantity.Should().Be(-500_000m);
        position.AveragePrice.Should().Be(99.50m);
        position.RealisedPnl.Should().Be(10_000m);
        position.PnlCurrency.Should().Be(new CurrencyCode("GBP"));

        var movement = await dbContext.PositionMovements
            .SingleAsync(x => x.TradeId == tradeId);

        movement.PreviousNetQuantity.Should().Be(1_000_000m);
        movement.PreviousAveragePrice.Should().Be(98.50m);

        movement.NewNetQuantity.Should().Be(-500_000m);
        movement.NewAveragePrice.Should().Be(99.50m);

        movement.PreviousRealisedPnl.Should().Be(0m);
        movement.RealisedPnlChange.Should().Be(10_000m);
        movement.NewRealisedPnl.Should().Be(10_000m);

        movement.AssetClass.Should().Be(AssetClass.FixedIncome);
        movement.PnlCurrency.Should().Be(new CurrencyCode("GBP"));

        messageContext.PublishedMessages.Should().HaveCount(1);

        var published = messageContext.PublishedMessages.Single().Message
            .Should()
            .BeOfType<PositionUpdated>()
            .Subject;

        published.NetQuantity.Should().Be(-500_000m);
        published.AveragePrice.Should().Be(99.50m);
        published.RealisedPnl.Should().Be(10_000m);
        published.AssetClass.Should().Be(AssetClass.FixedIncome);
        published.PnlCurrency.Should().Be("GBP");
    }

    private static PositionDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<PositionDbContext>()
            .UseSqlite(connection)
            .Options;

        return new SqlitePositionDbContext(options);
    }

    private static TradeCapturedHandler CreateHandler(PositionDbContext dbContext)
    {
        var unitOfWork = new EfUnitOfWork(
            dbContext,
            new PositionRepository(dbContext, NullLogger<PositionRepository>.Instance),
            new ProcessedTradeRepository(dbContext, NullLogger<ProcessedTradeRepository>.Instance),
            new PositionMovementRepository(dbContext, NullLogger<PositionMovementRepository>.Instance));

        var realisedPnlCalculatorResolver =
            new RealisedPnlCalculatorResolver(
                new IRealisedPnlCalculator[]
                {
                    new FxRealisedPnlCalculator(),
                    new EquityRealisedPnlCalculator(),
                    new BondRealisedPnlCalculator()
                });

        var positionCalculator =
            new PositionCalculator(realisedPnlCalculatorResolver);

        return new TradeCapturedHandler(
            unitOfWork,
            positionCalculator,
            NullLogger<TradeCapturedHandler>.Instance);
    }
}