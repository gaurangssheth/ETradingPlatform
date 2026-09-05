using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.PositionAccounting
{
    public sealed class BondUnrealisedPnlCalculator : IUnrealisedPnlCalculator
    {
        private const decimal PricePerHundredNominal = 100m;

        public AssetClass AssetClass => AssetClass.FixedIncome;

        public decimal Calculate(
            decimal netQuantity,
            decimal averagePrice,
            decimal markPrice)
        {
            // Unrealised P&L is valued as if the position were closed now.
            // Long positions close by selling at the Bid.
            // Short positions close by buying at the Ask.
            // markPrice has already been selected accordingly by MarkPriceSelector.

            // Bond prices are quoted per 100 nominal, so P&L must be scaled by
            // PricePerHundredNominal after applying the long/short price difference.
            if (netQuantity > 0)
            {
                return netQuantity
                    * (markPrice - averagePrice) / PricePerHundredNominal;
            }

            if (netQuantity < 0)
            {
                return Math.Abs(netQuantity)
                    * (averagePrice - markPrice) / PricePerHundredNominal;
            }

            return 0m;
        }
    }
}
