namespace PricingService.Grpc.MarketData
{
    public sealed record MarketCandle(
        string Symbol,
        DateTimeOffset StartTime,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close
    );
}
