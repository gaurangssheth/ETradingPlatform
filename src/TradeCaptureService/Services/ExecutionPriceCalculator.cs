using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Pricing;
using TradingApp.Contracts.Shared;

namespace TradeCaptureService.Services
{
    public sealed class ExecutionPriceCalculator
    {
        public decimal Calculate(OrderSide side, PriceQuote quote)
        {
            return side switch
            {
                OrderSide.Buy => quote.Ask,
                OrderSide.Sell => quote.Bid,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(side),
                    side,
                    "Unsupported order side.")
            };
        }
    }
}
