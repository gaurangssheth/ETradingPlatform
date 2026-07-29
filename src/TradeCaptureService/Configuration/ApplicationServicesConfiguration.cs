using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Calculations;
using TradeCaptureService.Pricing;
using TradeCaptureService.ReferenceData;
using TradeCaptureService.Services;

namespace TradeCaptureService.Configuration
{
    public static class ApplicationServicesConfiguration
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            var pricingServiceUrl = configuration["PricingService:Url"];
            var referenceDataServiceUrl = configuration["ReferenceDataService:Url"];

            if (string.IsNullOrWhiteSpace(pricingServiceUrl))
            {
                throw new InvalidOperationException(
                    "PricingService:Url configuration is missing.");
            }

            if (string.IsNullOrWhiteSpace(referenceDataServiceUrl))
            {
                throw new InvalidOperationException(
                    "ReferenceDataService:Url configuration is missing.");
            }

            services.AddGrpcClient<PricingService.Grpc.Pricing.PricingClient>(options =>
            {
                options.Address = new Uri(pricingServiceUrl);
            });

            services.AddGrpcClient<ReferenceDataService.Grpc.ReferenceData.ReferenceDataClient>(
            options =>
            {
                options.Address = new Uri(referenceDataServiceUrl);
            });

            services.AddScoped<IPricingClient, GrpcPricingClient>();
            services.AddScoped<IReferenceDataClient, GrpcReferenceDataClient>();
            services.AddSingleton<ExecutionPriceCalculator>();

            services.AddSingleton<
                INotionalCalculator,
                FxNotionalCalculator>();

            services.AddSingleton<
                INotionalCalculator,
                EquityNotionalCalculator>();

            services.AddSingleton<
                INotionalCalculator,
                BondNotionalCalculator>();

            services.AddSingleton<
                NotionalCalculatorResolver>();

            return services;
        }
    }
}
