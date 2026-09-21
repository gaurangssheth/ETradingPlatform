namespace TradingGateway.Api.ClientModels
{
    public class MarketQuoteResponse
    {
        public string Symbol { get; init; } = string.Empty;
        public decimal Bid { get; init; }
        public decimal Ask { get; init; }
        public decimal Spread => Ask - Bid;
        public DateTimeOffset Timestamp { get; init; }

    }
}
