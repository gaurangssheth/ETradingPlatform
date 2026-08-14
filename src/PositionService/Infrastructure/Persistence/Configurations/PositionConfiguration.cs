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
            entity.ToTable("Positions", table =>
            {
                table.HasCheckConstraint(
                    "CK_Positions_AssetClass",
                    "[AssetClass] COLLATE Latin1_General_100_BIN2 " +
                    "IN ('Fx', 'Equity', 'FixedIncome')");

                table.HasCheckConstraint(
                    "CK_Positions_InstrumentId_NotEmpty",
                    "[InstrumentId] <> '00000000-0000-0000-0000-000000000000'");

                table.HasCheckConstraint(
                    "CK_Positions_PnlCurrency",
                    "LEN([PnlCurrency]) = 3 AND " +
                    "[PnlCurrency] COLLATE Latin1_General_100_BIN2 " +
                    "NOT LIKE '%[^A-Z]%'");
            });

            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientId).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => new {e.ClientId, e.InstrumentId}).IsUnique();
            entity.Property(e => e.InstrumentId).IsRequired();
            entity.Property(e => e.AssetClass).HasConversion<string>().HasMaxLength(20)
                .IsUnicode(false).IsRequired();
            entity.Property(e => e.Symbol).HasMaxLength(20).IsRequired();
            entity.Property(e => e.NetQuantity).HasPrecision(18,4).IsRequired();
            entity.Property(e => e.AveragePrice).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.PnlCurrency).HasConversion(
                currency => currency.Value,
                value => new CurrencyCode(value))
                .HasMaxLength(3).IsUnicode(false).IsFixedLength().IsRequired();
            entity.Property(e => e.RealisedPnl).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.UnrealisedPnl).HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(100).IsRequired();
            entity.HasMany(e => e.Movements).WithOne(e => e.Position).HasForeignKey(e => e.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
