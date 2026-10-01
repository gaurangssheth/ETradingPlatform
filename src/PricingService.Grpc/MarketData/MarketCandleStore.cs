namespace PricingService.Grpc.MarketData
{
    public sealed class MarketCandleStore
    {
        private const int MaxHistory = 500;
        private object sync = new();

        private readonly Dictionary<string, MarketCandle> currentCandles = new();
        private readonly Dictionary<string, Queue<MarketCandle>> candleHistory = new();

        public MarketCandle? GetCurrent(string symbol)
        {
            lock (sync)
            {
                return currentCandles.TryGetValue(symbol, out var candle) ? candle : null;
            }
        }

        public MarketCandle? SaveCurrent(MarketCandle candle)
        {
            lock (sync)
            {
                return currentCandles[candle.Symbol] = candle;
            }
        }

        public void Complete(MarketCandle candle)
        {
            lock (this.sync)
            {
                if (!this.candleHistory.TryGetValue(candle.Symbol, out var history))
                {
                    history = new Queue<MarketCandle>();
                    this.candleHistory[candle.Symbol] = history;
                }

                history.Enqueue(candle);

                if (history.Count > MaxHistory)
                {
                    history.Dequeue();
                }

                this.currentCandles.Remove(candle.Symbol);
            }
        }

        public IReadOnlyList<MarketCandle> GetCandles(string symbol)
        {
            lock (this.sync)
            {
                var candles = this.candleHistory.TryGetValue(symbol, out var history)
                    ? history.ToList()
                    : new List<MarketCandle>();

                if (this.currentCandles.TryGetValue(symbol, out var currentCandle))
                {
                    candles.Add(currentCandle);
                }

                return candles;
            }
        }
    }
}
