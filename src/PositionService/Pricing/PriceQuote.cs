using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Pricing
{
    public sealed record PriceQuote(
        string Symbol,
        decimal Bid,
        decimal Ask,
        decimal Mid
    );
}
