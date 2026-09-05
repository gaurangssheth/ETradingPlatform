using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PositionService.Application.PositionAccounting;
using PositionService.Domain;
using PositionService.Infrastructure.Repositories;
using PositionService.Infrastructure.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;
using TradingApp.SharedKernel;

namespace PositionService.Tests.Application.PositionAccounting
{
    public class UnrealisedPnlPriceTickProcessorTests
    {
        [Fact]
        public async Task ProcessAsync_ShouldUpdateLongAndShortPositionsFromPriceTick()
        {
            var longPosition = CreatePosition(
                netQuantity: 100000m,
                averagePrice: 1.0850m);

            var shortPosition = CreatePosition(
                netQuantity: -50000m,
                averagePrice: 1.0860m);

            var positions = new List<Position>
            {
                longPosition,
                shortPosition
            };

            var positionRepository = new Mock<IPositionRepository>();

            positionRepository.Setup(repository =>
                repository.GetOpenPositionsBySymbolAsync("EURUSD", It.IsAny<CancellationToken>()))
                .ReturnsAsync(positions);

            var unitOfWork = new Mock<IUnitOfWork>();

            unitOfWork
                .SetupGet(x => x.Positions)
                .Returns(positionRepository.Object);

            var calculator = CreateMarkToMarketCalculator();

            var processor = new UnrealisedPnlPriceTickProcessor(
                calculator, unitOfWork.Object, NullLogger<UnrealisedPnlPriceTickProcessor>.Instance);

            var tick = new PriceTick(
                Symbol: "EURUSD",
                Bid: 1.0870m,
                Ask: 1.0872m,
                Timestamp: DateTimeOffset.UtcNow);

            await processor.ProcessAsync(tick);

            longPosition.UnrealisedPnl.Should().Be(200m);
            shortPosition.UnrealisedPnl.Should().Be(-60m);

            unitOfWork.Verify(
                x => x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);

        }

        private static Position CreatePosition(decimal netQuantity, decimal averagePrice)
        {
            return new Position
            {
                Id = Guid.NewGuid(),
                ClientId = "Client1",
                InstrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Symbol = "EURUSD",
                AssetClass = AssetClass.Fx,
                NetQuantity = netQuantity,
                AveragePrice = averagePrice,
                PnlCurrency = new CurrencyCode("USD"),
                RealisedPnl = 0m,
                UnrealisedPnl = 0m,
                CorrelationId = "price-tick-test",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        private static PositionMarkToMarketCalculator CreateMarkToMarketCalculator()
        {
            var calculators =
                new IUnrealisedPnlCalculator[]
                {
                    new FxUnrealisedPnlCalculator(),
                    new EquityUnrealisedPnlCalculator(),
                    new BondUnrealisedPnlCalculator()
                };

            var resolver =
                new UnrealisedPnlCalculatorResolver(
                    calculators);

            return new PositionMarkToMarketCalculator(
                new MarkPriceSelector(),
                resolver);
        }
    }
}
