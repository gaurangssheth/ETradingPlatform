using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.PositionAccounting
{
    public sealed class FxUnrealisedPnlCalculator : IUnrealisedPnlCalculator
    {
        public AssetClass AssetClass => AssetClass.Fx;

        public decimal Calculate(
            decimal netQuantity,
            decimal averagePrice,
            decimal markPrice)
        {
            // Unrealised P&L is valued as if the position were closed now.
            // Long positions close by selling at the Bid.
            // Short positions close by buying at the Ask.
            // markPrice has already been selected accordingly by MarkPriceSelector.
            if (netQuantity > 0)
            {
                return netQuantity
                    * (markPrice - averagePrice);
            }

            if (netQuantity < 0)
            {
                return Math.Abs(netQuantity)
                    * (averagePrice - markPrice);
            }

            return 0m;
        }
    }
}
