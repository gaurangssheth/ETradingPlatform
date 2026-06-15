using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;

namespace OrderService.Domain
{
    public class Order
    {
        public Guid Id { get; set; }

        public string ClientId { get; set; } = null!;

        public string Symbol { get; set; } = null!;

        public OrderSide Side { get; set; }

        public OrderType OrderType { get; set; }

        public decimal Quantity { get; set; }

        public decimal Price { get; set; }

        public decimal Notional { get; set; }

        public string Status { get; set; } = null!;

        public string CorrelationId { get; set; } = null!;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? AcceptedAt { get; set; }

        public DateTimeOffset? RejectedAt { get; set; }

        public string? RejectionReason { get; set; }
    }
}
