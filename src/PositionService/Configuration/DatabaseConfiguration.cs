using Microsoft.EntityFrameworkCore;
using PositionService.Infrastructure.Persistence;
using PositionService.Infrastructure.Repositories;
using PositionService.Infrastructure.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.ConnnectionStringNames;
using TradingApp.Shared.Diagnostics;

namespace PositionService.Configuration
{
    public static class DatabaseConfiguration
    {
        public static IServiceCollection AddPositionDatabase(this IServiceCollection services,
            IConfiguration configuration)
        {
            var positionDb = configuration.GetConnectionString(ConnectionStringNames.PositionDb)
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{ConnectionStringNames.PositionDb}");

            services.AddDbContext<PositionDbContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(positionDb);
                if (configuration.GetValue<bool>("EfCore:EnableSensitiveDataLogging"))
                {
                    options.EnableSensitiveDataLogging();
                }

                if (configuration.GetValue<bool>("EfCore:EnableDetailedErrors"))
                {
                    options.EnableDetailedErrors();
                }
                if (configuration.GetValue<bool>("EfCore:EnableInlineSqlLogging"))
                {
                    options.AddInterceptors(
                        serviceProvider.GetRequiredService<InlineSqlLoggingInterceptor>());
                }

            });

            services.AddSingleton<InlineSqlLoggingInterceptor>();
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IPositionRepository, PositionRepository>();
            services.AddScoped<IProcessedTradeRepository, ProcessedTradeRepository>();
            services.AddScoped<IPositionMovementRepository, PositionMovementRepository>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            return services;
        }
    }
}
