using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Repositories;
using OrderService.Infrastructure.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.ConnnectionStringNames;

namespace OrderService.Configuration
{
    public static class DatabaseConfiguration
    {
        public static IServiceCollection AddOrderDatabase(this IServiceCollection services,
            IConfiguration configuration)
        {
            var orderDb = configuration.GetConnectionString(ConnectionStringNames.OrderDb)
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{ConnectionStringNames.OrderDb}");

            services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(orderDb));

            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            return services;
        }
    }
}
