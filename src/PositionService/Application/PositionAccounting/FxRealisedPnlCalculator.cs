using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.PositionAccounting
{
    public sealed class FxRealisedPnlCalculator : IRealisedPnlCalculator
    {
        public AssetClass AssetClass => AssetClass.Fx;

        public decimal Calculate(decimal closedQuantity, decimal priceDifference)
        {
            if (closedQuantity <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    "closedQuantity",
                    "Closed quantity must be greater than zero.");
            }

            return closedQuantity * priceDifference;
        }
    }
}
