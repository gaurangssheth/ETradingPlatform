using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;

namespace PositionService.MarketData
{
    public class PriceTickBuffer
    {
        private readonly ConcurrentDictionary<string, PriceTick> latestTicks = new();

        private readonly Channel<bool> signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropWrite
        });

        public void Publish(PriceTick tick)
        {
            latestTicks.AddOrUpdate(tick.Symbol, tick, (_, existingTick) => 
                tick.Timestamp > existingTick.Timestamp ? tick : existingTick);

            signal.Writer.TryWrite(true);
        }

        public async Task WaitForUpdatesAsync(CancellationToken cancellationToken)
        {
            await signal.Reader.ReadAsync(cancellationToken);
        }

        public IReadOnlyCollection<PriceTick> TakeLatest()
        {
            var ticks = new List<PriceTick>();
            
            foreach (var symbol in latestTicks.Keys)
            {
                if (latestTicks.TryRemove(symbol, out var tick))
                {
                    ticks.Add(tick);
                }
            }

            return ticks;
        }
    }
}
