using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Pricing;
using TradeCaptureService.Services;

namespace TradeCaptureService.Configuration
{
    public static class ApplicationServicesConfiguration
    {
        public static IServiceCollection AddPositionApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            var privingServiceUrl = configuration["PricingService:Url"];

            if (string.IsNullOrWhiteSpace(privingServiceUrl))
            {
                throw new InvalidOperationException(
                    "PricingService:Url configuration is missing.");
            }

            services.AddGrpcClient<PricingService.Grpc.Pricing.PricingClient>(options =>
            {
                options.Address = new Uri(privingServiceUrl);
            });

            services.AddScoped<IPricingClient, GrpcPricingClient>();
            services.AddSingleton<ExecutionPriceCalculator>();

            return services;
        }
    }
}
