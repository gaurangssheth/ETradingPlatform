using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCaptureService.Pricing
{
    public sealed class PriceQuote
    {
        public string Symbol { get; set; } = null!;

        public decimal Bid { get; init; }

        public decimal Ask { get; init; }

        public decimal Mid { get; init; }

    }
}
