using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PositionService.Domain;
using PositionService.Infrastructure.Persistence;
using PositionService.Tests.Infrastructure.Persistence;
using TradingApp.Contracts.Shared;
using TradingApp.SharedKernel;

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

        await using var dbContext = new SqlitePositionDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var positionId = Guid.NewGuid();
        var instrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var position = new Position
        {
            Id = positionId,
            ClientId = "client-001",
            InstrumentId = instrumentId,
            Symbol = "EURUSD",
            AssetClass = AssetClass.Fx,
            NetQuantity = 60m,
            AveragePrice = 1.0800m,
            PnlCurrency = new CurrencyCode("USD"),
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
            InstrumentId = instrumentId,
            Symbol = "EURUSD",
            Side = OrderSide.Buy,
            AssetClass = AssetClass.Fx,
            Quantity = 100m,
            SignedQuantity = 100m,
            Price = 1.0800m,
            PreviousNetQuantity = 0m,
            PreviousAveragePrice = 0m,
            NewNetQuantity = 100m,
            NewAveragePrice = 1.0800m,
            PreviousRealisedPnl = 0m,
            RealisedPnlChange = 0m,
            NewRealisedPnl = 0m,
            PnlCurrency = new CurrencyCode("USD"),
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
            InstrumentId = instrumentId,
            Symbol = "EURUSD",
            Side = OrderSide.Sell,
            AssetClass = AssetClass.Fx,
            Quantity = 40m,
            SignedQuantity = -40m,
            Price = 1.0900m,
            PreviousNetQuantity = 100m,
            PreviousAveragePrice = 1.0800m,
            NewNetQuantity = 60m,
            NewAveragePrice = 1.0800m,
            PreviousRealisedPnl = 0m,
            RealisedPnlChange = 0.4000m,
            NewRealisedPnl = 0.4000m,
            PnlCurrency = new CurrencyCode("USD"),
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
            .Sum(x => x.RealisedPnlChange)
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

        await using var dbContext = new SqlitePositionDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var movementWithoutPosition = new PositionMovement
        {
            Id = Guid.NewGuid(),
            // Deliberately invalid: there is no Position with this Id.
            PositionId = Guid.NewGuid(),
            TradeId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            ClientId = "client-001",
            InstrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Symbol = "EURUSD",
            AssetClass = AssetClass.Fx,
            Side = OrderSide.Buy,
            Quantity = 100m,
            SignedQuantity = 100m,
            Price = 1.0800m,
            PreviousNetQuantity = 0m,
            PreviousAveragePrice = 0m,
            NewNetQuantity = 100m,
            NewAveragePrice = 1.0800m,
            PreviousRealisedPnl = 0m,
            RealisedPnlChange = 0m,
            NewRealisedPnl = 0m,
            PnlCurrency = new CurrencyCode("USD"),
            CorrelationId = "relationship-test-invalid",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.PositionMovements.Add(movementWithoutPosition);

        var action = async () => await dbContext.SaveChangesAsync();

        await action.Should().ThrowAsync<DbUpdateException>();
    }
}
