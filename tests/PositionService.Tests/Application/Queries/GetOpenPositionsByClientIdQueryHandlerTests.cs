using FluentAssertions;
using Moq;
using PositionService.Application.Queries;
using PositionService.Domain;
using PositionService.Infrastructure.Repositories;
using PositionService.Infrastructure.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.SharedKernel;

namespace PositionService.Tests.Application.Queries
{
    public class GetOpenPositionsByClientIdQueryHandlerTests
    {
        [Fact]
        public async Task HandleAsync_ShouldReturnMappedOpenPositionsByClient()
        {
            var positions = new List<Position>
            {
                CreatePosition(
                    symbol: "EURUSD",
                    netQuantity: 100000m,
                    averagePrice: 1.0850m,
                    realisedPnl: 25m,
                    unrealisedPnl: 75m),
                CreatePosition(
                    symbol: "AAPL",
                    netQuantity: 10m,
                    averagePrice: 210m,
                    realisedPnl: -5m,
                    unrealisedPnl: 20m)
            };

            var positionRepositiroy = new Mock<IPositionRepository>();

            positionRepositiroy.Setup(
                repository => repository.GetOpenPositionsByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(positions);

            var unitOfWork = new Mock<IUnitOfWork>();

            unitOfWork.Setup(unitOfWork => unitOfWork.Positions).Returns(positionRepositiroy.Object);

            var handler = new GetOpenPositionsByClientIdQueryHandler(unitOfWork.Object);

            var result = await handler.HandleAsync(
                new GetOpenPositionsByClientIdQuery("client-001"), CancellationToken.None);

            result.Should().HaveCount(2);

            var eurusdPosition = result.FirstOrDefault(p => p.Symbol == "EURUSD");
            eurusdPosition.Should().NotBeNull();
            eurusdPosition.NetQuantity.Should().Be(100000m);
            eurusdPosition.AveragePrice.Should().Be(1.0850m);
            eurusdPosition.RealisedPnl.Should().Be(25m);
            eurusdPosition.UnrealisedPnl.Should().Be(75m);
            eurusdPosition.TotalPnl.Should().Be(100m);

            var aaplPosition = result.FirstOrDefault(p => p.Symbol == "AAPL");
            aaplPosition.Should().NotBeNull();
            aaplPosition.NetQuantity.Should().Be(10m);
            aaplPosition.AveragePrice.Should().Be(210m);
            aaplPosition.RealisedPnl.Should().Be(-5m);
            aaplPosition.UnrealisedPnl.Should().Be(20m);
            aaplPosition.TotalPnl.Should().Be(15m);
        }

        private static Position CreatePosition(
            string symbol,
            decimal netQuantity,
            decimal averagePrice,
            decimal realisedPnl,
            decimal unrealisedPnl)
        {
            var instrumentId = symbol switch
            {
                "EURUSD" => Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"),

                "AAPL" => Guid.Parse(
                    "22222222-2222-2222-2222-222222222222"),

                "GB00TEST1234" => Guid.Parse(
                    "33333333-3333-3333-3333-333333333333"),

                _ => Guid.NewGuid()
            };

            var assetClass = symbol switch
            {
                "EURUSD" => AssetClass.Fx,
                "AAPL" => AssetClass.Equity,
                "GB00TEST1234" => AssetClass.FixedIncome,
                _ => AssetClass.Fx
            };

            var pnlCurrency = symbol switch
            {
                "EURUSD" => new CurrencyCode("USD"),
                "AAPL" => new CurrencyCode("USD"),
                "GB00TEST1234" => new CurrencyCode("GBP"),
                _ => new CurrencyCode("USD")
            };

            return new Position
            {
                Id = Guid.NewGuid(),
                ClientId = "client-001",
                InstrumentId = instrumentId,
                Symbol = symbol,
                AssetClass = assetClass,
                NetQuantity = netQuantity,
                AveragePrice = averagePrice,
                PnlCurrency = pnlCurrency,
                RealisedPnl = realisedPnl,
                UnrealisedPnl = unrealisedPnl,
                AccountingVersion = 0,
                CorrelationId = "query-test-001",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
