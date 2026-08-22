namespace PricingService.Grpc.MarketData
{
    public sealed class PriceTickSubscriberHostedService : BackgroundService
    {
        private readonly PriceTickSubscriberWorker worker;

        public PriceTickSubscriberHostedService(
            PriceTickSubscriberWorker worker)
        {
            this.worker = worker;
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => this.worker.Run(cancellationToken), cancellationToken);
        }
    }
}
