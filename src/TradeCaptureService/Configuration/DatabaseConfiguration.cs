using Microsoft.EntityFrameworkCore;
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

namespace TradeCaptureService.Configuration
{
    public static class DatabaseConfiguration
    {
        public static IServiceCollection AddTradeCaptureDatabase(this IServiceCollection services,
            IConfiguration configuration)
        {
            var tradeCaptureDb = configuration.GetConnectionString(ConnectionStringNames.TradeCaptureDb)
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{ConnectionStringNames.TradeCaptureDb}");

            services.AddDbContext<TradeDbContext>(options => options.UseSqlServer(tradeCaptureDb));

            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            //services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<ITradeRepository, TradeRepository>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            return services;
        }
    }
}
