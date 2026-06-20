using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PositionService.Domain;
using PositionService.Infrastructure.Persistence;
using TradingApp.Contracts.Shared;

namespace PositionService.Tests;

public class PositionEfRelationshipTests
{
    [Fact]
    public async Task Position_ShouldHaveManyPositionMovements()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PositionDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new PositionDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var positionId = Guid.NewGuid();

        var position = new Position
        {
            Id = positionId,
            ClientId = "client-001",
            Symbol = "EURUSD",
            NetQuantity = 60m,
            AveragePrice = 1.0800m,
            RealisedPnl = 0.4000m,
            UnrealisedPnl = 0m,
            CorrelationId = "relationship-test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var movement1 = new PositionMovement
        {
            Id = Guid.NewGuid(),
            PositionId = positionId,
            TradeId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            ClientId = "client-001",
            Symbol = "EURUSD",
            Side = OrderSide.Buy,
            Quantity = 100m,
            SignedQuantity = 100m,
            Price = 1.0800m,
            PreviousNetQuantity = 0m,
            PreviousAveragePrice = 0m,
            NewNetQuantity = 100m,
            NewAveragePrice = 1.0800m,
            RealisedPnl = 0m,
            CorrelationId = "relationship-test-001",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var movement2 = new PositionMovement
        {
            Id = Guid.NewGuid(),
            PositionId = positionId,
            TradeId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            ClientId = "client-001",
            Symbol = "EURUSD",
            Side = OrderSide.Sell,
            Quantity = 40m,
            SignedQuantity = -40m,
            Price = 1.0900m,
            PreviousNetQuantity = 100m,
            PreviousAveragePrice = 1.0800m,
            NewNetQuantity = 60m,
            NewAveragePrice = 1.0800m,
            RealisedPnl = 0.4000m,
            CorrelationId = "relationship-test-002",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Positions.Add(position);
        dbContext.PositionMovements.AddRange(movement1, movement2);

        await dbContext.SaveChangesAsync();

        var loadedPosition = await dbContext.Positions
            .Include(x => x.Movements)
            .SingleAsync(x => x.Id == positionId);

        loadedPosition.Movements.Should().HaveCount(2);

        loadedPosition.Movements
            .Select(x => x.TradeId)
            .Should()
            .Contain(new[] { movement1.TradeId, movement2.TradeId });

        loadedPosition.Movements
            .Sum(x => x.RealisedPnl)
            .Should()
            .Be(0.4000m);
    }

    [Fact]
    public async Task PositionMovement_ShouldRequireValidPositionId()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PositionDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new PositionDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var movementWithoutPosition = new PositionMovement
        {
            Id = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            TradeId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            ClientId = "client-001",
            Symbol = "EURUSD",
            Side = OrderSide.Buy,
            Quantity = 100m,
            SignedQuantity = 100m,
            Price = 1.0800m,
            PreviousNetQuantity = 0m,
            PreviousAveragePrice = 0m,
            NewNetQuantity = 100m,
            NewAveragePrice = 1.0800m,
            RealisedPnl = 0m,
            CorrelationId = "relationship-test-invalid",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.PositionMovements.Add(movementWithoutPosition);

        var action = async () => await dbContext.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }
}
