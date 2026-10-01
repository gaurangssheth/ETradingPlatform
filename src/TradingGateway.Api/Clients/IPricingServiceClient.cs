using TradingGateway.Api.ClientModels;

namespace TradingGateway.Api.Clients
{
    public interface IPricingServiceClient
    {
        Task<IReadOnlyList<MarketQuoteResponse>> GetMarketQuotesAsync(
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        IAsyncEnumerable<MarketQuoteResponse> StreamMarketQuotesAsync(
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MarketCandleResponse>> GetMarketCandlesAsync(
            string symbol,
            string? correlationId = null,
            CancellationToken cancellationToken = default);

        IAsyncEnumerable<MarketCandleResponse> StreamMarketCandlesAsync(
            string? correlationId = null,
            CancellationToken cancellationToken = default);
    }
}
