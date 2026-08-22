using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;

namespace MarketDataSimulator
{
    internal sealed class InstrumentPriceSimulator
    {
        private readonly ChannelWriter<PriceTick> writer;
        private readonly string symbol;
        private readonly decimal spread;
        private readonly decimal priceStep;
        private readonly int minimumDelayMilliseconds;
        private readonly int maximumDelayMilliseconds;

        private decimal bid;

        public InstrumentPriceSimulator(
        ChannelWriter<PriceTick> writer,
        string symbol,
        decimal initialBid,
        decimal spread,
        decimal priceStep,
        int minimumDelayMilliseconds,
        int maximumDelayMilliseconds)
        {
            this.writer = writer;
            this.symbol = symbol;
            this.bid = initialBid;
            this.spread = spread;
            this.priceStep = priceStep;
            this.minimumDelayMilliseconds = minimumDelayMilliseconds;
            this.maximumDelayMilliseconds = maximumDelayMilliseconds;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"{this.symbol} simulator started.");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var direction = Random.Shared.Next(-1, 2);

                    this.bid += direction * this.priceStep;

                    var tick = new PriceTick(
                        Symbol: this.symbol,
                        Bid: this.bid,
                        Ask: this.bid + this.spread,
                        Timestamp: DateTimeOffset.UtcNow);

                    await writer.WriteAsync(tick, cancellationToken);

                    var delayMilliseconds =
                    Random.Shared.Next(
                        this.minimumDelayMilliseconds,
                        this.maximumDelayMilliseconds);

                    await Task.Delay(
                        delayMilliseconds,
                        cancellationToken);
                }

                Console.WriteLine($"{this.symbol}: while condition became false.");
            }
            finally
            {
                Console.WriteLine(
                    $"{this.symbol} simulator exiting. " +
                    $"CancellationRequested={cancellationToken.IsCancellationRequested}");
            }

        }

    }
}
