using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PositionService.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Infrastructure.Persistence.Configurations
{
    public class PositionConfiguration : IEntityTypeConfiguration<Position>
    {
        public void Configure(EntityTypeBuilder<Position> entity)
        {
            entity.ToTable("Positions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientId).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => new {e.ClientId, e.Symbol}).IsUnique();
            entity.Property(e => e.Symbol).HasMaxLength(20).IsRequired();
            entity.Property(e => e.NetQuantity).HasPrecision(18,4).IsRequired();
            entity.Property(e => e.AveragePrice).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.RealisedPnl).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.UnrealisedPnl).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(100).IsRequired();
            entity.HasMany(e => e.Movements).WithOne(e => e.Position).HasForeignKey(e => e.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
