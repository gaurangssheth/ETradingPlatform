using TradingGateway.Api.Application.Queries;
using TradingGateway.Api.ClientModels;
using TradingGateway.Api.Clients;

namespace TradingGateway.Api.Application.Queries.Pricing
{
    public sealed class GetMarketQuotesQueryHandler : IQueryHandler<GetMarketQuotesQuery, IReadOnlyList<MarketQuoteResponse>>
    {
        private readonly IPricingServiceClient pricingServiceClient;

        public GetMarketQuotesQueryHandler(IPricingServiceClient pricingServiceClient)
        {
            this.pricingServiceClient = pricingServiceClient;
        }

        public Task<IReadOnlyList<MarketQuoteResponse>> HandleAsync(GetMarketQuotesQuery query, CancellationToken cancellationToken)
        {
            return pricingServiceClient.GetMarketQuotesAsync(query.CorrelationId, cancellationToken);
        }
    }
}
