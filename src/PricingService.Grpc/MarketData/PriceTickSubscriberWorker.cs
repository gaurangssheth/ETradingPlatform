namespace PricingService.Grpc.MarketData
{
    public sealed class PriceTickSubscriberWorker
    {
        private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromMilliseconds(100);

        private readonly MarketQuoteCache latestQuoteStore;
        private readonly string endpoint;

        public PriceTickSubscriberWorker(MarketQuoteCache latestQuoteStore, string endpoint)
        {
            this.latestQuoteStore = latestQuoteStore;
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

                Console.WriteLine(
                    $"PricingService received {tick.Symbol} " +
                    $"Bid={tick.Bid} Ask={tick.Ask}");
            }

            Console.WriteLine(
                "Market data subscriber worker stopped.");
        }
    }
}
