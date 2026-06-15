using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Infrastructure.Persistence
{
    public class PositionDbContextFactory : IDbContextFactory<PositionDbContext>
    {
        public PositionDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<PositionDbContext>();

            optionsBuilder.UseSqlServer(
                "Server=localhost;Database=TradingApp_PositionDb;User Id=sa;Password=gaurang;Encrypt=True;TrustServerCertificate=True");

            return new PositionDbContext(optionsBuilder.Options);
        }
    }
}
