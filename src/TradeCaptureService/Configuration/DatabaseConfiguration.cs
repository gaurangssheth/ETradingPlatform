using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Infrastructure.Persistence;
using TradeCaptureService.Infrastructure.Repositories;
using TradeCaptureService.Infrastructure.UnitOfWork;
using TradeCaptureService.Repositories;
using TradingApp.Shared.ConnnectionStringNames;
using TradingApp.Shared.Diagnostics;

namespace TradeCaptureService.Configuration
{
    public static class DatabaseConfiguration
    {
        public static IServiceCollection AddTradeCaptureDatabase(this IServiceCollection services,
            IConfiguration configuration)
        {
            var tradeCaptureDb = configuration.GetConnectionString(ConnectionStringNames.TradeCaptureDb)
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{ConnectionStringNames.TradeCaptureDb}");

            services.AddDbContext<TradeDbContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(tradeCaptureDb);
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
            services.AddScoped<ITradeRepository, TradeRepository>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            return services;
        }
    }
}
