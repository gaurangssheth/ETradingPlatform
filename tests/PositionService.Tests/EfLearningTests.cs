using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PositionService.Domain;
using PositionService.Infrastructure.Persistence;
using PositionService.Tests.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;
using TradingApp.SharedKernel;

namespace PositionService.Tests
{
    public class EfLearningTests
    {
        [Fact]
        public async Task WithoutInclude_MovementsAreNotLoadedInFreshContext()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<PositionDbContext>()
                .UseSqlite(connection)
                .LogTo(Console.WriteLine)
                .EnableSensitiveDataLogging()
                .Options;

            var positionId = Guid.NewGuid();

            await using (var dbContext = new SqlitePositionDbContext(options))
            {
                await dbContext.Database.EnsureCreatedAsync();

                var position = CreatePosition(positionId);
                var movement = CreateMovement(positionId);

                dbContext.Positions.Add(position);
                dbContext.PositionMovements.Add(movement);

                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new SqlitePositionDbContext(options))
            {
                var loadedPosition = await dbContext.Positions
                    .SingleAsync(x => x.Id == positionId);

                loadedPosition.Movements.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task WithInclude_MovementsAreLoaded()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<PositionDbContext>()
                .UseSqlite(connection)
                .Options;

            var positionId = Guid.NewGuid();

            await using (var dbContext = new SqlitePositionDbContext(options))
            {
                await dbContext.Database.EnsureCreatedAsync();

                dbContext.Positions.Add(CreatePosition(positionId));
                dbContext.PositionMovements.Add(CreateMovement(positionId));

                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new SqlitePositionDbContext(options))
            {
                var loadedPosition = await dbContext.Positions
                    .Include(x => x.Movements)
                    .SingleAsync(x => x.Id == positionId);

                loadedPosition.Movements.Should().HaveCount(1);
            }
        }

        [Fact]
        public async Task Projection_CanReturnCustomShape()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<PositionDbContext>()
                .UseSqlite(connection)
                .Options;

            var positionId = Guid.NewGuid();

            await using (var dbContext = new SqlitePositionDbContext(options))
            {
                await dbContext.Database.EnsureCreatedAsync();

                dbContext.Positions.Add(CreatePosition(positionId));
                dbContext.PositionMovements.Add(CreateMovement(positionId));

                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new SqlitePositionDbContext(options))
            {
                var result = await dbContext.Positions
                    .Where(x => x.Id == positionId)
                    .Select(x => new
                    {
                        x.ClientId,
                        x.Symbol,
                        MovementCount = x.Movements.Count,
                        RealisedPnl = x.Movements.Sum(m => (double)m.RealisedPnlChange)
                    })
                    .SingleAsync();

                result.ClientId.Should().Be("client-001");
                result.Symbol.Should().Be("EURUSD");
                result.MovementCount.Should().Be(1);
                result.RealisedPnl.Should().Be(0.0);
            }
        }

        [Fact]
        public async Task TrackedPosition_WhenPropertiesChange_ShouldBeUpdatedBySaveChanges()
        {
            await using var connection =
                new SqliteConnection("DataSource=:memory:");

            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<PositionDbContext>()
                .UseSqlite(connection)
                .LogTo(Console.WriteLine)
                .EnableSensitiveDataLogging()
                .Options;

            var positionId = Guid.NewGuid();

            await using (var dbContext = new SqlitePositionDbContext(options))
            {
                await dbContext.Database.EnsureCreatedAsync();

                dbContext.Positions.Add(
                    CreatePosition(positionId));

                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new SqlitePositionDbContext(options))
            {
                var position = await dbContext.Positions
                    .SingleAsync(x => x.Id == positionId);

                // EF loaded it, so EF is now tracking it.
                dbContext.Entry(position).State
                    .Should().Be(EntityState.Unchanged);

                position.NetQuantity = 60m;
                position.AveragePrice = 1.0900m;
                position.RealisedPnl = 0.4000m;

                // Force EF to compare original values with current values.
                dbContext.ChangeTracker.DetectChanges();

                var entry = dbContext.Entry(position);

                entry.State.Should().Be(EntityState.Modified);

                entry.Property(x => x.NetQuantity)
                    .OriginalValue.Should().Be(100m);

                entry.Property(x => x.NetQuantity)
                    .CurrentValue.Should().Be(60m);

                entry.Property(x => x.NetQuantity)
                    .IsModified.Should().BeTrue();

                entry.Property(x => x.AveragePrice)
                    .IsModified.Should().BeTrue();

                entry.Property(x => x.RealisedPnl)
                    .IsModified.Should().BeTrue();

                // We never changed Symbol.
                entry.Property(x => x.Symbol)
                    .IsModified.Should().BeFalse();

                await dbContext.SaveChangesAsync();

                // Once saved, the current values become the new baseline.
                entry.State.Should().Be(EntityState.Unchanged);
            }

            await using (var dbContext =
                new SqlitePositionDbContext(options))
            {
                var savedPosition = await dbContext.Positions
                    .SingleAsync(x => x.Id == positionId);

                savedPosition.NetQuantity.Should().Be(60m);
                savedPosition.AveragePrice.Should().Be(1.0900m);
                savedPosition.RealisedPnl.Should().Be(0.4000m);
            }
        }

        private static Position CreatePosition(Guid positionId)
        {
            return new Position
            {
                Id = positionId,
                ClientId = "client-001",
                InstrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Symbol = "EURUSD",
                AssetClass = AssetClass.Fx,
                NetQuantity = 100m,
                AveragePrice = 1.0800m,
                PnlCurrency = new CurrencyCode("USD"),
                RealisedPnl = 0m,
                UnrealisedPnl = 0m,
                CorrelationId = "ef-learning",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        private static PositionMovement CreateMovement(Guid positionId)
        {
            return new PositionMovement
            {
                Id = Guid.NewGuid(),
                PositionId = positionId,
                TradeId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                ClientId = "client-001",
                InstrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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
                CorrelationId = "ef-learning",
                CreatedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
