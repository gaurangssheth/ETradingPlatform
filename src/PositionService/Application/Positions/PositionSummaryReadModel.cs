using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.Positions
{
    public sealed record PositionSummaryReadModel(
        Guid InstrumentId,
        string Symbol,
        AssetClass AssetClass,
        decimal NetQuantity,
        decimal AveragePrice,
        CurrencyCode PnlCurrency,
        decimal RealisedPnl,
        decimal UnrealisedPnl
    )
    {
        public decimal TotalPnl => RealisedPnl + UnrealisedPnl;
    }
}
