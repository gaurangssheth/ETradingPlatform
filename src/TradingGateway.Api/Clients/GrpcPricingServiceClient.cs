using Grpc.Core;
using PricingService.Grpc;
using System.Globalization;
using TradingApp.Shared.Messaging.Correlation;
using TradingGateway.Api.ClientModels;

namespace TradingGateway.Api.Clients
{
    public class GrpcPricingServiceClient : IPricingServiceClient
    {
        private readonly PricingService.Grpc.Pricing.PricingClient pricingClient;
        private readonly ILogger<GrpcPricingServiceClient> logger;


        public GrpcPricingServiceClient(PricingService.Grpc.Pricing.PricingClient pricingClient,
            ILogger<GrpcPricingServiceClient> logger)
        {
            this.pricingClient = pricingClient;
            this.logger = logger;
        }

        public async Task<IReadOnlyList<MarketQuoteResponse>> GetMarketQuotesAsync(string? correlationId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var headers = new Metadata();

                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    headers.Add(GrpcCorrelationConstants.MetadataKey, correlationId);
                }

                var response = await pricingClient.GetMarketQuotesAsync(
                    new GetMarketQuotesRequest(),
                    headers,
                    cancellationToken: cancellationToken);

                return response.Quotes.Select(x => new MarketQuoteResponse
                {
                    Symbol = x.Symbol,
                    Bid = Decimal.Parse(x.Bid, CultureInfo.InvariantCulture),
                    Ask = Decimal.Parse(x.Ask, CultureInfo.InvariantCulture),
                    Timestamp = DateTimeOffset.Parse(x.Timestamp, CultureInfo.InvariantCulture)
                }).ToList();
            }
            catch (RpcException ex)
            {
                logger.LogError(ex,
                    "Failed to retrieve market quotes from PricingService.");

                throw;
            }

        }
    }
}
