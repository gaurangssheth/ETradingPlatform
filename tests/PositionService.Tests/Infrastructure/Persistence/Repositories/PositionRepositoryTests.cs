using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PositionService.Domain;
using PositionService.Infrastructure.Persistence;
using PositionService.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.SharedKernel;

namespace PositionService.Tests.Infrastructure.Persistence.Repositories
{
    public class PositionRepositoryTests
    {
        [Fact]
        public async Task GetOpenPositionsBySymbolAsync_ShouldReturnOnlyOpenPositionsForSymbol()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<PositionDbContext>()
                .UseSqlite(connection)
                .Options;

            // Arrange
            await using (var setupDbContext = new SqlitePositionDbContext(options))
            {
                await setupDbContext.Database.EnsureCreatedAsync();

                await setupDbContext.Positions.AddRangeAsync(
                    CreatePosition(
                        Guid.NewGuid(),
                        "client-001",
                        "EURUSD",
                        100m),
                    CreatePosition(
                        Guid.NewGuid(),
                        "client-002",
                        "EURUSD",
                        -50m),
                    CreatePosition(
                        Guid.NewGuid(),
                        "client-003",
                        "EURUSD",
                        0m),
                    CreatePosition(
                        Guid.NewGuid(),
                        "client-004",
                        "AAPL",
                        10m));

                await setupDbContext.SaveChangesAsync();
            }

            await using (var queryDbContext = new SqlitePositionDbContext(options))
            {
                var repository = new PositionRepository(queryDbContext, NullLogger<PositionRepository>.Instance);

                // Act
                var positions = await repository.GetOpenPositionsBySymbolAsync("EURUSD");

                // Assert
                positions.Should().HaveCount(2);
                positions.Should().OnlyContain(
                    position =>
                        position.Symbol == "EURUSD" &&
                        position.NetQuantity != 0m);
            }
        }

        private static Position CreatePosition(Guid positionId, string clientId, string symbol, decimal netQuantity)
        {
            return new Position
            {
                Id = positionId,
                ClientId = clientId,
                InstrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Symbol = symbol,
                AssetClass = AssetClass.Fx,
                NetQuantity = netQuantity,
                AveragePrice = 1.0800m,
                PnlCurrency = new CurrencyCode("USD"),
                RealisedPnl = 0m,
                UnrealisedPnl = 0m,
                CorrelationId = "repository-test",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
