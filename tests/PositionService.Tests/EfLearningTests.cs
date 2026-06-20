using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PositionService.Domain;
using PositionService.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;

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

            await using (var setupContext = new PositionDbContext(options))
            {
                await setupContext.Database.EnsureCreatedAsync();

                var position = CreatePosition(positionId);
                var movement = CreateMovement(positionId);

                setupContext.Positions.Add(position);
                setupContext.PositionMovements.Add(movement);

                await setupContext.SaveChangesAsync();
            }

            await using (var queryContext = new PositionDbContext(options))
            {
                var loadedPosition = await queryContext.Positions
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

            await using (var setupContext = new PositionDbContext(options))
            {
                await setupContext.Database.EnsureCreatedAsync();

                setupContext.Positions.Add(CreatePosition(positionId));
                setupContext.PositionMovements.Add(CreateMovement(positionId));

                await setupContext.SaveChangesAsync();
            }

            await using (var queryContext = new PositionDbContext(options))
            {
                var loadedPosition = await queryContext.Positions
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

            await using (var setupContext = new PositionDbContext(options))
            {
                await setupContext.Database.EnsureCreatedAsync();

                setupContext.Positions.Add(CreatePosition(positionId));
                setupContext.PositionMovements.Add(CreateMovement(positionId));

                await setupContext.SaveChangesAsync();
            }

            await using (var queryContext = new PositionDbContext(options))
            {
                var result = await queryContext.Positions
                    .Where(x => x.Id == positionId)
                    .Select(x => new
                    {
                        x.ClientId,
                        x.Symbol,
                        MovementCount = x.Movements.Count,
                        RealisedPnl = x.Movements.Sum(m => (double)m.RealisedPnl)
                    })
                    .SingleAsync();

                result.ClientId.Should().Be("client-001");
                result.Symbol.Should().Be("EURUSD");
                result.MovementCount.Should().Be(1);
                result.RealisedPnl.Should().Be(0.0);
            }
        }

        private static Position CreatePosition(Guid positionId)
        {
            return new Position
            {
                Id = positionId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                NetQuantity = 100m,
                AveragePrice = 1.0800m,
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
                CorrelationId = "ef-learning",
                CreatedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
