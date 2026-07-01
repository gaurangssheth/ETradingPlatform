using Grpc.Core;
using PricingService.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;

namespace TradeCaptureService.Pricing
{
    public sealed class GrpcPricingClient : IPricingClient
    {
        private PricingService.Grpc.Pricing.PricingClient pricingClient;

        public GrpcPricingClient(PricingService.Grpc.Pricing.PricingClient pricingClient)
        {
            this.pricingClient = pricingClient;
        }

        public async Task<PriceQuote> GetPriceAsync(
            string symbol,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            var headers = new Metadata();

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                headers.Add(GrpcCorrelationConstants.MetadataKey, correlationId);
            }

            var resposne = await pricingClient.GetPriceAsync(new GetPriceRequest
            {
                Symbol = symbol
            }, headers: headers, cancellationToken: cancellationToken);

            return new PriceQuote
            {
                Symbol = resposne.Symbol,
                Bid = Convert.ToDecimal(resposne.Bid),
                Ask = Convert.ToDecimal(resposne.Ask),
                Mid = Convert.ToDecimal(resposne.Mid)
            };
        }
    }
}
