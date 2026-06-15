using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;

namespace TradingApp.Contracts.Events
{
    public class OrderRejected : IEvent
    {
        public Guid OrderId { get; set; }

        public string ClientId { get; set; } = null!;

        public string Symbol { get; set; } = null!;

        public OrderSide Side { get; set; }

        public OrderType OrderType { get; set; }

        public decimal Quantity { get; set; }

        public string Reason { get; set; } = null!;

        public DateTimeOffset RejectedAt { get; set; }

        public string CorrelationId { get; set; } = null!;
    }
}
