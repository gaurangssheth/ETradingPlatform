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
    public class ProcessedTradeConfiguration : IEntityTypeConfiguration<ProcessedTrade>
    {
        public void Configure(EntityTypeBuilder<ProcessedTrade> entity)
        {
            entity.ToTable("ProcessedTrades");
            entity.HasKey(e => e.TradeId);
            entity.HasIndex(e => e.OrderId);
            entity.Property(e => e.ClientId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Symbol).HasMaxLength(20).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(100).IsRequired();
        }
    }
}
