using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using TradingApp.MarketData.Contracts;

namespace PricingService.Grpc.MarketData
{
    public sealed class MarketQuoteCache
    {
        private readonly ConcurrentDictionary<string, PriceTick> quotes = new(StringComparer.OrdinalIgnoreCase);

        public void Update(PriceTick tick)
        {
            quotes.AddOrUpdate(tick.Symbol, tick, (_, existingTick) =>
                tick.Timestamp > existingTick.Timestamp ? tick : existingTick);
        }

        public bool TryGet(string symbol, [NotNullWhen(true)] out PriceTick? tick)
        {
            ArgumentNullException.ThrowIfNull(symbol, nameof(symbol));
            return this.quotes.TryGetValue(symbol, out tick);
        }
    }
}
