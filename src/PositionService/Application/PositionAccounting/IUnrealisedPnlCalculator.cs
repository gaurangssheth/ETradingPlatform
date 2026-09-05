using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.PositionAccounting
{
    public interface IUnrealisedPnlCalculator
    {
        AssetClass AssetClass { get; }

        decimal Calculate(
            decimal netQuantity,
            decimal averagePrice,
            decimal markPrice);
    }
}
