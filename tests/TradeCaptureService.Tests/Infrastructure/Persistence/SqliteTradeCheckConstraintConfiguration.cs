using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Domain;
using TradingApp.Contracts.Shared;


namespace TradeCaptureService.Tests.Infrastructure.Persistence
{
    internal sealed class SqliteTradeCheckConstraintConfiguration : IEntityTypeConfiguration<Trade>
    {
        public void Configure(EntityTypeBuilder<Trade> entity)
        {
            entity.ToTable("Trades", table =>
            {
                table.HasCheckConstraint(
                    "CK_Trades_AssetClass",
                    "[AssetClass] " +
                    "IN ('Fx', 'Equity', 'FixedIncome')");

                table.HasCheckConstraint(
                    "CK_Trades_Side",
                    "[Side] " +
                    "IN ('Buy', 'Sell')");

                table.HasCheckConstraint(
                    "CK_Trades_OrderType",
                    "[OrderType] " +
                    "IN ('Market', 'Limit')");

                table.HasCheckConstraint(
                    "CK_Trades_Status",
                    "[Status] " +
                    "IN ('Captured', 'Cancelled', 'Amended')");

                table.HasCheckConstraint(
                    "CK_Trades_NotionalCurrency",
                    "length([NotionalCurrency]) = 3 AND " +
                    "[NotionalCurrency] " +
                    "NOT GLOB '*[^A-Z]*'");
            });
        }
    }
}
