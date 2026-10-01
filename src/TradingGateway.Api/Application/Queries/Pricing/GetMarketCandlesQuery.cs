namespace TradingGateway.Api.Application.Queries.Pricing
{
    public sealed record GetMarketCandlesQuery(
        string Symbol,
        string? CorrelationId);
}
