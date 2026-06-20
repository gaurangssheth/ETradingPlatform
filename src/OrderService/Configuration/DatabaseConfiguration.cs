using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Repositories;
using OrderService.Infrastructure.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.ConnnectionStringNames;
using TradingApp.Shared.Diagnostics;

namespace OrderService.Configuration
{
    public static class DatabaseConfiguration
    {
        public static IServiceCollection AddOrderDatabase(this IServiceCollection services,
            IConfiguration configuration)
        {
            var orderDb = configuration.GetConnectionString(ConnectionStringNames.OrderDb)
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{ConnectionStringNames.OrderDb}");

            services.AddDbContext<OrderDbContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(orderDb);
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
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            return services;
        }
    }
}
