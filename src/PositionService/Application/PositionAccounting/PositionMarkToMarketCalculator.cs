using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.PositionAccounting
{
    public class PositionMarkToMarketCalculator
    {
        private readonly MarkPriceSelector markPriceSelector;
        private readonly UnrealisedPnlCalculatorResolver unrealisedPnlCalculatorResolver;

        public PositionMarkToMarketCalculator(
            MarkPriceSelector markPriceSelector, 
            UnrealisedPnlCalculatorResolver unrealisedPnlCalculatorResolver)
        {
            this.markPriceSelector = markPriceSelector;
            this.unrealisedPnlCalculatorResolver = unrealisedPnlCalculatorResolver;
        }

        public decimal Calculate(
            AssetClass assetClass,
            decimal netQuantity,
            decimal averagePrice,
            decimal bid,
            decimal ask)
        {
            if (netQuantity == 0)
            {
                return 0;
            }

            var markPrice = this.markPriceSelector.GetMarkPrice(netQuantity, bid, ask);

            var calculator = this.unrealisedPnlCalculatorResolver.Resolve(assetClass);

            return calculator.Calculate(netQuantity, averagePrice, markPrice);
        }
    }
}
