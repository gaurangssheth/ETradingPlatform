using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Infrastructure.Persistence
{
    public class PositionDbContextFactory : IDesignTimeDbContextFactory<PositionDbContext>
    {
        public PositionDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PositionDbContext>();

            optionsBuilder.UseSqlServer(
                "Server=localhost;Database=TradingApp_PositionDb;User Id=sa;Password=gaurang;Encrypt=True;TrustServerCertificate=True");

            return new PositionDbContext(optionsBuilder.Options);
        }
    }
}
