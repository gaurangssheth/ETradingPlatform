using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCaptureService.Infrastructure.Persistence
{
    public sealed class TradeDbContextFactory : IDesignTimeDbContextFactory<TradeDbContext>
    {
        public TradeDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TradeDbContext>();

            optionsBuilder.UseSqlServer(
                "Server=localhost;Database=TradingApp_TradeCaptureDb;User Id=sa;Password=gaurang;Encrypt=True;TrustServerCertificate=True");

            return new TradeDbContext(optionsBuilder.Options);
        }
    }
}
