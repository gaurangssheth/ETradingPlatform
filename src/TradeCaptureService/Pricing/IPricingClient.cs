using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCaptureService.Pricing
{
    public interface IPricingClient
    {
        Task<PriceQuote> GetPriceAsync(
            string symbol,
            string? correlationId = null,
            CancellationToken cancellationToken = default);
    }
}
