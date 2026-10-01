using System.Threading.Channels;
using TradingApp.MarketData.Contracts;

namespace PricingService.Grpc.MarketData
{
    public sealed class MarketQuoteStream
    {
        private readonly Channel<PriceTick> channel = Channel.CreateUnbounded<PriceTick>();

        public bool Publish(PriceTick tick)
        {
            return channel.Writer.TryWrite(tick);
        }

        public IAsyncEnumerable<PriceTick> ReadAllAsync(CancellationToken cancellationToken) 
        {
            return channel.Reader.ReadAllAsync(cancellationToken);
        }
    }
}
