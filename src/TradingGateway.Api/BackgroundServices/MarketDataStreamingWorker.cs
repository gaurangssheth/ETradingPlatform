using Microsoft.AspNetCore.SignalR;
using TradingGateway.Api.Clients;
using TradingGateway.Api.Hubs;

namespace TradingGateway.Api.BackgroundServices
{
    public class MarketDataStreamingWorker : BackgroundService
    {
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly IHubContext<MarketDataHub> hubContext;
        private readonly ILogger<MarketDataStreamingWorker> logger;

        public MarketDataStreamingWorker(
            IServiceScopeFactory serviceScopeFactory,
            IHubContext<MarketDataHub> hubContext,
            ILogger<MarketDataStreamingWorker> logger)
        {
            this.serviceScopeFactory = serviceScopeFactory;
            this.hubContext = hubContext;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Market data streaming worker started.");

            using var scope = serviceScopeFactory.CreateScope();

            var pricingServiceClient = scope.ServiceProvider.GetRequiredService<IPricingServiceClient>();

            var correlationId = Guid.NewGuid().ToString();

            try
            {
                var quotesStreamingTask = StreamQuotesAsync(pricingServiceClient, correlationId, cancellationToken);
                var candlesStreamingTask = StreamCandlesAsync(pricingServiceClient, correlationId, cancellationToken);

                await Task.WhenAll(quotesStreamingTask, candlesStreamingTask);
            }
            catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
            {
                // Expected during application shutdown.
            }
            finally
            {
                logger.LogInformation("Market data streaming worker stopped.");
            }
        }

        private async Task StreamQuotesAsync(
            IPricingServiceClient pricingServiceClient,
            string correlationId,
            CancellationToken cancellationToken)
        {
            await foreach (var quote in pricingServiceClient.StreamMarketQuotesAsync(
                    correlationId,
                    cancellationToken: cancellationToken))
            {
                await hubContext.Clients.All.SendAsync("MarketQuoteUpdated", quote, cancellationToken);
            }
        }

        private async Task StreamCandlesAsync(
            IPricingServiceClient pricingServiceClient,
            string correlationId,
            CancellationToken cancellationToken)
        {
            await foreach (var candle in pricingServiceClient.StreamMarketCandlesAsync(
                    correlationId,
                    cancellationToken: cancellationToken))
            {
                await hubContext.Clients.All.SendAsync("MarketCandleUpdated", candle, cancellationToken);
            }
        }
    }
}
