using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;

namespace TradingApp.Contracts.Events
{
    public sealed class TradeCaptured : IEvent, ICorrelatedMessage
    {
        public Guid TradeId { get; set; }

        public Guid OrderId { get; set; }

        public string ClientId { get; set; } = null!;

        public string Symbol { get; set; } = null!;

        public OrderSide Side { get; set; }

        public decimal Quantity { get; set; }

        public decimal Price { get; set; }

        public decimal Notional { get; set; }

        public TradeStatus Status { get; set; }

        public DateTimeOffset CapturedAt { get; set; }

        public string CorrelationId { get; set; } = null!;
    }
}
