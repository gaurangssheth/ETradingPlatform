using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderService.Infrastructure.Persistence.Configurations
{
    public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> entity)
        {
            entity.ToTable("Orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Symbol).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Side).HasConversion<string>().IsRequired();
            entity.Property(e => e.OrderType).HasConversion<string>().IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
        }
    }
}
