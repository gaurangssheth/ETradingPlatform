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
        public static IServiceCollection AddPositionApplicationServices(this IServiceCollection services)
        {
            services.AddSingleton<PositionCalculator>();

            return services;
        }
    }
}
