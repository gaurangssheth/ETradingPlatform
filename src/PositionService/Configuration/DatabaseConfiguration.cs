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

namespace PositionService.Configuration
{
    public static class DatabaseConfiguration
    {
        public static IServiceCollection AddPositionDatabase(this IServiceCollection services,
            IConfiguration configuration)
        {
            var positionDb = configuration.GetConnectionString(ConnectionStringNames.PositionDb)
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{ConnectionStringNames.PositionDb}");

            services.AddDbContext<PositionDbContext>(options => options.UseSqlServer(positionDb));

            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IPositionRepository, PositionRepository>();
            services.AddScoped<IProcessedTradeRepository, ProcessedTradeRepository>();
            services.AddScoped<IPositionMovementRepository, PositionMovementRepository>();
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();

            return services;
        }
    }
}
