using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Domain;

namespace TradeCaptureService.Infrastructure.Persistence.Configurations
{
    public class TradeConfiguration : IEntityTypeConfiguration<Trade>
    {
        public void Configure(EntityTypeBuilder<Trade> entity)
        {
            entity.ToTable("Trades", table =>
            {
                table.HasCheckConstraint(
                    "CK_Trades_AssetClass",
                    "[AssetClass] COLLATE Latin1_General_100_BIN2 " +
                    "IN ('Fx', 'Equity', 'FixedIncome')");

                table.HasCheckConstraint(
                    "CK_Trades_Side",
                    "[Side] COLLATE Latin1_General_100_BIN2 " +
                    "IN ('Buy', 'Sell')");

                table.HasCheckConstraint(
                    "CK_Trades_OrderType",
                    "[OrderType] COLLATE Latin1_General_100_BIN2 " +
                    "IN ('Market', 'Limit')");

                table.HasCheckConstraint(
                    "CK_Trades_Status",
                    "[Status] COLLATE Latin1_General_100_BIN2 " +
                    "IN ('Captured', 'Cancelled', 'Amended')");

                table.HasCheckConstraint(
                    "CK_Trades_InstrumentId_NotEmpty",
                    "[InstrumentId] <> " +
                    "'00000000-0000-0000-0000-000000000000'");

                table.HasCheckConstraint(
                    "CK_Trades_NotionalCurrency",
                    "LEN([NotionalCurrency]) = 3 AND " +
                    "[NotionalCurrency] COLLATE Latin1_General_100_BIN2 " +
                    "NOT LIKE '%[^A-Z]%'");

                table.HasCheckConstraint(
                    "CK_Trades_Quantity_Positive",
                    "[Quantity] > 0");

                table.HasCheckConstraint(
                    "CK_Trades_Price_Positive",
                    "[Price] > 0");

                table.HasCheckConstraint(
                    "CK_Trades_Notional_Positive",
                    "[Notional] > 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId).IsUnique();
            entity.Property(e => e.InstrumentId).IsRequired();
            entity.HasIndex(e => e.InstrumentId);
            entity.Property(e => e.ClientId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Symbol).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AssetClass).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
            entity.Property(e => e.Side).HasConversion<string>().HasMaxLength(10).IsRequired();
            entity.Property(e => e.OrderType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.Price).HasPrecision(18, 8);
            entity.Property(e => e.Notional).HasPrecision(18, 4);
            entity.Property(e => e.NotionalCurrency)
                .HasConversion(
                    CurrencyCode => CurrencyCode.Value,
                    databaseValue => new CurrencyCode(databaseValue))
                .HasMaxLength(3).IsUnicode(false).IsFixedLength().IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(100).IsRequired();
        }
    }
}
