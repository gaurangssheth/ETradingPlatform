using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.PositionAccounting
{
    public interface IRealisedPnlCalculator
    {
        AssetClass AssetClass { get; }

        decimal Calculate(decimal closedQuantity, decimal priceDifference);
    }
}
