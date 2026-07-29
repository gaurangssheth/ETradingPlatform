using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Domain;
using TradeCaptureService.Infrastructure.Persistence;

namespace TradeCaptureService.Tests.Infrastructure.Persistence
{
    internal sealed class SqliteTradeDbContext : TradeDbContext
    {
        public SqliteTradeDbContext(DbContextOptions<TradeDbContext> options): base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var entity = modelBuilder.Entity<Trade>();

            entity.Metadata.RemoveCheckConstraint(
            "CK_Trades_AssetClass");

            entity.Metadata.RemoveCheckConstraint(
                "CK_Trades_Side");

            entity.Metadata.RemoveCheckConstraint(
                "CK_Trades_OrderType");

            entity.Metadata.RemoveCheckConstraint(
                "CK_Trades_Status");

            entity.Metadata.RemoveCheckConstraint(
                "CK_Trades_NotionalCurrency");

            new SqliteTradeCheckConstraintConfiguration()
                .Configure(entity);
        }
    }
}
