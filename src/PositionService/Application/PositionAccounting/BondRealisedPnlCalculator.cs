using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.PositionAccounting
{
    public sealed class BondRealisedPnlCalculator : IRealisedPnlCalculator
    {
        private const decimal PricePerHundredNominal = 100m;

        public AssetClass AssetClass => AssetClass.FixedIncome;

        public decimal Calculate(
            decimal closedQuantity,
            decimal priceDifference)
        {
            if (closedQuantity <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    "closedQuantity",
                    "Closed quantity must be greater than zero.");
            }

            return closedQuantity *
                   priceDifference /
                   PricePerHundredNominal;
        }
    }
}
