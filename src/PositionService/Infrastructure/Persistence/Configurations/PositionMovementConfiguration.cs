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
            entity.ToTable("PositionMovements", table =>
            {
                table.HasCheckConstraint(
                    "CK_PositionMovements_AssetClass",
                    "[AssetClass] COLLATE Latin1_General_100_BIN2 " +
                    "IN ('Fx', 'Equity', 'FixedIncome')");

                table.HasCheckConstraint(
                    "CK_PositionMovements_InstrumentId_NotEmpty",
                    "[InstrumentId] <> '00000000-0000-0000-0000-000000000000'");

                table.HasCheckConstraint(
                    "CK_PositionMovements_PnlCurrency",
                    "LEN([PnlCurrency]) = 3 AND " +
                    "[PnlCurrency] COLLATE Latin1_General_100_BIN2 " +
                    "NOT LIKE '%[^A-Z]%'");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TradeId).IsUnique();
            entity.HasIndex(e => new { e.ClientId, e.InstrumentId });
            entity.Property(e => e.ClientId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.InstrumentId).IsRequired();
            entity.Property(e => e.Symbol).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Side).HasConversion<string>().HasMaxLength(10).IsRequired();
            entity.Property(e => e.AssetClass).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.SignedQuantity).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.Price).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.PreviousNetQuantity).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.PreviousAveragePrice).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.NewNetQuantity).HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.NewAveragePrice).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.PreviousRealisedPnl).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.RealisedPnlChange).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.NewRealisedPnl).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.PnlCurrency).HasConversion(
                    currency => currency.Value,
                    value => new CurrencyCode(value))
                .HasMaxLength(3).IsUnicode(false).IsFixedLength().IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(100).IsRequired();


        }
    }
}
