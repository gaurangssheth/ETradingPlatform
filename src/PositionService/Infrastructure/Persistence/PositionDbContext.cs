using Microsoft.EntityFrameworkCore;
using PositionService.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Infrastructure.Persistence
{
    public class PositionDbContext : DbContext
    {
        public PositionDbContext(DbContextOptions<PositionDbContext> options) : base(options)
        {
        }

        public DbSet<Position> Positions => Set<Position>();

        public DbSet<ProcessedTrade> ProcessedTrades => Set<ProcessedTrade>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(PositionDbContext).Assembly);
        }
    }
}
