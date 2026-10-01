using System.Threading.Channels;

namespace PricingService.Grpc.MarketData
{
    public sealed class MarketCandleStream
    {
        private readonly Channel<MarketCandle> channel = Channel.CreateUnbounded<MarketCandle>();

        public bool Publish(MarketCandle candle)
        {
            return this.channel.Writer.TryWrite(candle);
        }

        public IAsyncEnumerable<MarketCandle> ReadAllAsync(CancellationToken cancellation)
        {
            return this.channel.Reader.ReadAllAsync(cancellation);
        }
    }
}
