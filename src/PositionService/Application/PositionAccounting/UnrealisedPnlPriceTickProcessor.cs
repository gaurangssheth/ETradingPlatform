using PositionService.Domain;
using PositionService.Infrastructure.UnitOfWork;
using PositionService.Pricing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;

namespace PositionService.Application.PositionAccounting
{
    public class UnrealisedPnlPriceTickProcessor
    {
        private readonly PositionMarkToMarketCalculator positionMarkToMarketCalculator;
        private readonly IUnitOfWork unitOfWork;
        private ILogger<UnrealisedPnlPriceTickProcessor> logger;

        public UnrealisedPnlPriceTickProcessor(
            PositionMarkToMarketCalculator positionMarkToMarketCalculator,
            IUnitOfWork unitOfWork,            
            ILogger<UnrealisedPnlPriceTickProcessor> logger)
        {
            this.unitOfWork = unitOfWork;
            this.positionMarkToMarketCalculator = positionMarkToMarketCalculator;
            this.logger = logger;
        }

        public async Task ProcessAsync(PriceTick tick, CancellationToken cancellationToken = default)
        {
            var openPositions = await unitOfWork.Positions.GetOpenPositionsBySymbolAsync(
                tick.Symbol, cancellationToken);

            if (openPositions.Count == 0)
            {
                return;
            }

            foreach (var position in openPositions)
            {
                position.UnrealisedPnl = positionMarkToMarketCalculator.Calculate(
                    position.AssetClass, position.NetQuantity, position.AveragePrice, tick.Bid, tick.Ask);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogTrace(
                "Marked {PositionCount} open positions for {Symbol} using Bid={Bid}, Ask={Ask}.",
                openPositions.Count,
                tick.Symbol,
                tick.Bid,
                tick.Ask);


        }
    }
}
