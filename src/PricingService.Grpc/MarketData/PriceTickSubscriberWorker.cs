namespace PricingService.Grpc.MarketData
{
    public sealed class PriceTickSubscriberWorker
    {
        private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromMilliseconds(100);

        private readonly MarketQuoteCache latestQuoteStore;
        private readonly MarketQuoteStream marketQuoteStream;
        private readonly CandleAggregator candleAggregator;
        private readonly MarketCandleStream marketCandleStream;
        private readonly string endpoint;

        public PriceTickSubscriberWorker(
            MarketQuoteCache latestQuoteStore,
            MarketQuoteStream marketQuoteStream,
            CandleAggregator candleAggregator,
            MarketCandleStream marketCandleStream,
            string endpoint)
        {
            this.latestQuoteStore = latestQuoteStore;
            this.marketQuoteStream = marketQuoteStream;
            this.candleAggregator = candleAggregator;
            this.marketCandleStream = marketCandleStream;
            this.endpoint = endpoint;
        }

        public void Run(CancellationToken cancellationToken)
        {
            using var subscriber = new ZeroMqPriceSubscriber(this.endpoint);
            while (!cancellationToken.IsCancellationRequested)
            {
                var received  = subscriber.TryReceive(ReceiveTimeout, out var tick);

                if (!received)
                {
                    continue;
                }

                if (tick is null)
                {
                    continue;
                }

                this.latestQuoteStore.Update(tick);
                this.marketQuoteStream.Publish(tick);

                var candle = this.candleAggregator.Add(tick);
                this.marketCandleStream.Publish(candle);

                Console.WriteLine(
                    $"PricingService received {tick.Symbol} " +
                    $"Bid={tick.Bid} Ask={tick.Ask}");

                Console.WriteLine(
                    $"Candle {candle.Symbol} " +
                    $"Start={candle.StartTime:HH:mm:ss} " +
                    $"O={candle.Open} H={candle.High} " +
                    $"L={candle.Low} C={candle.Close}");
            }

            Console.WriteLine(
                "Market data subscriber worker stopped.");
        }
    }
}
