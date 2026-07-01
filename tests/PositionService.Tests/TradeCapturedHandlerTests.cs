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
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;

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

        var message = new TradeCaptured
        {
            TradeId = tradeId,
            OrderId = orderId,
            ClientId = "client-001",
            Symbol = "EURUSD",
            Side = OrderSide.Buy,
            Quantity = 100m,
            Price = 1.0800m,
            CapturedAt = DateTimeOffset.UtcNow,
            CorrelationId = "handler-test-001"
        };

        await handler.Handle(message, messageContext);

        var position = await dbContext.Positions.SingleAsync(
            x => x.ClientId == message.ClientId && x.Symbol == message.Symbol);

        position.ClientId.Should().Be("client-001");
        position.Symbol.Should().Be("EURUSD");
        position.NetQuantity.Should().Be(100m);
        position.AveragePrice.Should().Be(1.0800m);
        position.RealisedPnl.Should().Be(0m);
        position.UnrealisedPnl.Should().Be(0m);
        position.CorrelationId.Should().Be("handler-test-001");

        var movement = await dbContext.PositionMovements.SingleAsync(x => x.TradeId == tradeId);

        movement.PositionId.Should().Be(position.Id);
        movement.TradeId.Should().Be(tradeId);
        movement.OrderId.Should().Be(orderId);
        movement.Side.Should().Be(OrderSide.Buy);
        movement.Quantity.Should().Be(100m);
        movement.SignedQuantity.Should().Be(100m);
        movement.Price.Should().Be(1.0800m);
        movement.PreviousNetQuantity.Should().Be(0m);
        movement.PreviousAveragePrice.Should().Be(0m);
        movement.NewNetQuantity.Should().Be(100m);
        movement.NewAveragePrice.Should().Be(1.0800m);
        movement.RealisedPnl.Should().Be(0m);
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
        published.Symbol.Should().Be("EURUSD");
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

        var existingPosition = new Position
        {
            Id = Guid.NewGuid(),
            ClientId = "client-001",
            Symbol = "EURUSD",
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
            Symbol = "EURUSD",
            Side = OrderSide.Sell,
            Quantity = 40m,
            Price = 1.0900m,
            CapturedAt = DateTimeOffset.UtcNow,
            CorrelationId = "handler-test-002"
        };

        await handler.Handle(message, messageContext);

        var position = await dbContext.Positions.SingleAsync(
            x => x.ClientId == message.ClientId && x.Symbol == message.Symbol);

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
        movement.RealisedPnl.Should().Be(0.4000m);

        await dbContext.ProcessedTrades
            .Where(x => x.TradeId == tradeId)
            .CountAsync()
            .ContinueWith(t => t.Result.Should().Be(1));

        messageContext.PublishedMessages.Should().HaveCount(1);

        var published = messageContext.PublishedMessages.Single().Message
            .Should()
            .BeOfType<PositionUpdated>()
            .Subject;

        published.NetQuantity.Should().Be(60m);
        published.AveragePrice.Should().Be(1.0800m);
        published.RealisedPnl.Should().Be(0.4000m);
    }

    private static PositionDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<PositionDbContext>()
            .UseSqlite(connection)
            .Options;

        return new PositionDbContext(options);
    }

    private static TradeCapturedHandler CreateHandler(PositionDbContext dbContext)
    {
        var unitOfWork = new EfUnitOfWork(
            dbContext,
            new PositionRepository(dbContext, NullLogger<PositionRepository>.Instance),
            new ProcessedTradeRepository(dbContext, NullLogger<ProcessedTradeRepository>.Instance),
            new PositionMovementRepository(dbContext, NullLogger<PositionMovementRepository>.Instance));

        return new TradeCapturedHandler(
            unitOfWork,
            new PositionCalculator(),
            NullLogger<TradeCapturedHandler>.Instance);
    }
}