namespace TradingGateway.Api.ClientModels
{
    public sealed record MarketCandleResponse(
        string Symbol,
        DateTimeOffset StartTime,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close);
}
