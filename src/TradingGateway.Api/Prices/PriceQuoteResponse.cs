namespace TradingGateway.Api.Prices
{
    public sealed class PriceQuoteResponse
    {
        public string Symbol { get; init; } = null!;

        public decimal Bid { get; init; }

        public decimal Ask { get; init; }

        public decimal Mid { get; init; }
    }
}
