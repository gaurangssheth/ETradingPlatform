using PositionService.Application.PositionAccounting;
using PositionService.BackgroundServices;
using PositionService.MarketData;
using PositionService.Pricing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Configuration
{
    public static class ApplicationServicesConfiguration
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            var pricingServiceUrl = configuration["PricingService:Url"];

            if (string.IsNullOrWhiteSpace(pricingServiceUrl))
            {
                throw new InvalidOperationException(
                    "PricingService:Url configuration is missing.");
            }

            services.AddGrpcClient<PricingService.Grpc.Pricing.PricingClient>(options =>
            {
                options.Address = new Uri(pricingServiceUrl);
            });

            services.AddSingleton<MarkPriceSelector>();
            services.AddScoped<IPricingClient, GrpcPricingClient>();
            services.AddSingleton<IRealisedPnlCalculator, FxRealisedPnlCalculator>();
            services.AddSingleton<IRealisedPnlCalculator, EquityRealisedPnlCalculator>();
            services.AddSingleton<IRealisedPnlCalculator, BondRealisedPnlCalculator>();
            
            services.AddSingleton<RealisedPnlCalculatorResolver>();

            services.AddSingleton<IUnrealisedPnlCalculator, FxUnrealisedPnlCalculator>();
            services.AddSingleton<IUnrealisedPnlCalculator, EquityUnrealisedPnlCalculator>();
            services.AddSingleton<IUnrealisedPnlCalculator, BondUnrealisedPnlCalculator>();

            services.AddSingleton<UnrealisedPnlCalculatorResolver>();

            services.AddSingleton<PositionCalculator>();
            services.AddSingleton<PositionMarkToMarketCalculator>();

            services.AddSingleton<PriceTickBuffer>();
            services.AddSingleton<ZeroMqPriceTickSubscriber>();
            services.AddHostedService<PriceTickSubscriberBackgroundWorker>();

            services.AddScoped<UnrealisedPnlPriceTickProcessor>();
            services.AddHostedService<UnrealisedPnlPriceTickBackgroundWorker>();

            return services;
        }
    }
}
