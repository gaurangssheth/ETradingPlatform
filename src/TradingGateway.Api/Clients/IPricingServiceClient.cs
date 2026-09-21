using TradingGateway.Api.ClientModels;

namespace TradingGateway.Api.Clients
{
    public interface IPricingServiceClient
    {
        Task<IReadOnlyList<MarketQuoteResponse>> GetMarketQuotesAsync(
            string? correlationId = null,
            CancellationToken cancellationToken = default);
    }
}
