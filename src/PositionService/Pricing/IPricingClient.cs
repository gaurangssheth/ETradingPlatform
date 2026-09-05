using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PositionService.Pricing
{
    public interface IPricingClient
    {
        Task<PriceQuote> GetPriceAsync(
            string symbol,
            string? correlationId = null,
            CancellationToken cancellationToken = default);
    }
}
