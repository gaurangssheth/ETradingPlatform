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
    public class PositionMovementConfiguration : IEntityTypeConfiguration<PositionMovement>
    {
        public void Configure(EntityTypeBuilder<PositionMovement> entity)
        {
            entity.ToTable("PositionMovements");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TradeId).IsUnique();
            entity.HasIndex(e => new { e.ClientId, e.Symbol });
            entity.Property(e => e.ClientId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ClientId).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Side).HasConversion<string>().HasMaxLength(10).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.SignedQuantity).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.Price).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.PreviousNetQuantity).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.PreviousAveragePrice).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.NewNetQuantity).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.NewAveragePrice).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.RealisedPnl).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(100).IsRequired();


        }
    }
}
