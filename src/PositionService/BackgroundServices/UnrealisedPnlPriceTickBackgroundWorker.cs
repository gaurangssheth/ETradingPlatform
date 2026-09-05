using Microsoft.EntityFrameworkCore;
using PositionService.Application.PositionAccounting;
using PositionService.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.BackgroundServices
{
    public class UnrealisedPnlPriceTickBackgroundWorker : BackgroundService
    {
        private readonly PriceTickBuffer priceTickBuffer;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<UnrealisedPnlPriceTickBackgroundWorker> logger;

        public UnrealisedPnlPriceTickBackgroundWorker(
            PriceTickBuffer priceTickBuffer,
            IServiceScopeFactory scopeFactory,
            ILogger<UnrealisedPnlPriceTickBackgroundWorker> logger)
        {
            this.priceTickBuffer = priceTickBuffer;
            this.scopeFactory = scopeFactory;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation(
                "Unrealised P&L price tick worker started.");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await priceTickBuffer.WaitForUpdatesAsync(cancellationToken);

                    var ticks = priceTickBuffer.TakeLatest();

                    var tasks = ticks.Select(async tick =>
                    {
                        try
                        {
                            using var scope = scopeFactory.CreateScope();

                            var processor = scope.ServiceProvider
                                    .GetRequiredService<UnrealisedPnlPriceTickProcessor>();

                            await processor.ProcessAsync(tick, cancellationToken);
                        }
                        catch (OperationCanceledException)
                            when (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            logger.LogDebug(
                                "Skipped stale MTM update for {Symbol} because the position changed during processing.",
                                tick.Symbol);
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(
                                exception,
                                "Failed to mark positions for {Symbol}.",
                                tick.Symbol);
                        }
                    });

                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            logger.LogInformation(
                "Unrealised P&L price tick worker stopped.");
        }
    }
}
