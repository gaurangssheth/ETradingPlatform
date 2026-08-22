namespace TradingApp.MarketData.Contracts
{
    public sealed record PriceTick(
        string Symbol,
        decimal Bid,
        decimal Ask,
        DateTimeOffset Timestamp
    );
}
