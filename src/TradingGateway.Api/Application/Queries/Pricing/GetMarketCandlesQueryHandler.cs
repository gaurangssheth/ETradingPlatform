namespace TradingGateway.Api.Application.Queries.Pricing
{
    using global::TradingGateway.Api.ClientModels;
    using global::TradingGateway.Api.Clients;

    namespace TradingGateway.Api.Application.Queries.Pricing
    {
        public sealed class GetMarketCandlesQueryHandler :
            IQueryHandler<
                GetMarketCandlesQuery,
                IReadOnlyList<MarketCandleResponse>>
        {
            private readonly IPricingServiceClient pricingServiceClient;

            public GetMarketCandlesQueryHandler(
                IPricingServiceClient pricingServiceClient)
            {
                this.pricingServiceClient = pricingServiceClient;
            }

            public Task<IReadOnlyList<MarketCandleResponse>> HandleAsync(
                GetMarketCandlesQuery query,
                CancellationToken cancellationToken)
            {
                return this.pricingServiceClient.GetMarketCandlesAsync(
                    query.Symbol,
                    query.CorrelationId,
                    cancellationToken);
            }
        }
    }
}
