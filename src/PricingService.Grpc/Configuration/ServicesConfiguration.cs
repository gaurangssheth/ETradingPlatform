using PricingService.Grpc.BackgroundServices;
using PricingService.Grpc.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PricingService.Configuration
{
    public static class ServicesConfiguration
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services, IConfiguration configuration)
        {
            var marketDataEndpoint = configuration.GetValue<string>("MarketData:Endpoint") ??
                throw new InvalidOperationException("Marketdata:Endpoint is not configured.");

            services.AddSingleton<MarketQuoteCache>();
            services.AddSingleton<PriceTickSubscriberWorker>(
                serviceProvider => new PriceTickSubscriberWorker(
                    serviceProvider.GetRequiredService<MarketQuoteCache>(),
                    serviceProvider.GetRequiredService<MarketQuoteStream>(),
                    serviceProvider.GetRequiredService<CandleAggregator>(),
                    serviceProvider.GetRequiredService<MarketCandleStream>(),
                    marketDataEndpoint));

            services.AddHostedService<PriceTickSubscriberHostedService>();
            services.AddSingleton<MarketQuoteStream>();
            services.AddSingleton<MarketCandleStream>();

            services.AddSingleton<MarketCandleStore>();
            services.AddSingleton<CandleAggregator>();

            return services;
        }
    }
}
