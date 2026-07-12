using RiskService.Grpc.Application;
using RiskService.Grpc.Application.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RiskService.Grpc.Configuration
{
    public static class ApplicationServicesConfiguration
    {
        public static IServiceCollection AddRiskApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IRiskRule, ClientActiveRiskRule>();
            services.AddSingleton<IRiskRule, ClientBlockedRiskRule>();
            services.AddSingleton<IRiskRule, SymbolAllowedRiskRule>();
            services.AddSingleton<IRiskRule, MaxOrderSizeRiskRule>();
            services.AddSingleton<RiskPolicyEngine>();

            return services;
        }
    }
}
