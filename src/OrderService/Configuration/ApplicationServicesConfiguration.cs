using OrderService.Risk;
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
            var privingServiceUrl = configuration["RiskService:Url"];

            if (string.IsNullOrWhiteSpace(privingServiceUrl))
            {
                throw new InvalidOperationException(
                    "RiskService:Url configuration is missing.");
            }

            services.AddGrpcClient<RiskService.Grpc.Risk.RiskClient>(options =>
            {
                options.Address = new Uri(privingServiceUrl);
            });

            services.AddScoped<IRiskClient, GrpcRiskClient>();

            return services;
        }
    }
}
