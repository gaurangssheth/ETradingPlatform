using PositionService.Application.PositionAccounting;
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
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddSingleton<IRealisedPnlCalculator, FxRealisedPnlCalculator>();
            services.AddSingleton<IRealisedPnlCalculator, EquityRealisedPnlCalculator>();
            services.AddSingleton<IRealisedPnlCalculator, BondRealisedPnlCalculator>();
            services.AddSingleton<RealisedPnlCalculatorResolver>();
            services.AddSingleton<PositionCalculator>();

            return services;
        }
    }
}
