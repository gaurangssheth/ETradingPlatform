using PositionService.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.BackgroundServices
{
    public class PriceTickSubscriberBackgroundWorker : BackgroundService
    {
        private readonly ZeroMqPriceTickSubscriber subscriber;
        private readonly IConfiguration configuration;
        private readonly ILogger<PriceTickSubscriberBackgroundWorker> logger;

        public PriceTickSubscriberBackgroundWorker(
            ZeroMqPriceTickSubscriber subscriber,
            IConfiguration configuration,
            ILogger<PriceTickSubscriberBackgroundWorker> logger)
        {
            this.subscriber = subscriber;
            this.configuration = configuration;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var marketDataEndpoint = configuration.GetValue<string>("MarketData:Endpoint") ??
                throw new InvalidOperationException("Marketdata:Endpoint is not configured.");

            logger.LogInformation("Starting ZeroMQ market data subscriber on {Endpoint}.", marketDataEndpoint);

            try
            {
                await Task.Run(
                    () => subscriber.Run(marketDataEndpoint, cancellationToken),
                    cancellationToken
                );
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("ZeroMQ market data subscriber stopped.");
            }
        }
    }
}
