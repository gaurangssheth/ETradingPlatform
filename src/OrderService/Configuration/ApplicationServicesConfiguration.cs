using OrderService.Pricing;
using OrderService.Risk;
using OrderService.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OrderService.Configuration
{
    public static class ApplicationServicesConfiguration
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            var riskServiceUrl = configuration["RiskService:Url"];
            var pricingServiceUrl = configuration["PricingService:Url"];

            if (string.IsNullOrWhiteSpace(riskServiceUrl))
            {
                throw new InvalidOperationException(
                    "RiskService:Url configuration is missing.");
            }

            if (string.IsNullOrWhiteSpace(pricingServiceUrl))
            {
                throw new InvalidOperationException(
                    "PricingService:Url configuration is missing.");
            }

            services.AddGrpcClient<RiskService.Grpc.Risk.RiskClient>(options =>
            {
                options.Address = new Uri(riskServiceUrl);
            });

            services.AddGrpcClient<PricingService.Grpc.Pricing.PricingClient>(options =>
            {
                options.Address = new Uri(pricingServiceUrl);
            });

            services.AddScoped<IRiskClient, GrpcRiskClient>();
            services.AddScoped<IPricingClient, GrpcPricingClient>();
            services.AddSingleton<LimitOrderExecutionEvaluator>();

            return services;
        }
    }
}
