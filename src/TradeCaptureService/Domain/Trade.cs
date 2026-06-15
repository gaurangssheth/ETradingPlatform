using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;

namespace TradeCaptureService.Domain
{
    public class Trade
    {
        public Guid Id { get; set; }

        public Guid OrderId { get; set; }

        public string ClientId { get; set; } = null!;

        public string Symbol { get; set; } = null!;

        public OrderSide Side { get; set; }

        public OrderType OrderType { get; set; }

        public decimal Quantity { get; set; }

        public decimal Price { get; set; }

        public decimal Notional { get; set; }

        public TradeStatus Status { get; set; }

        public string CorrelationId { get; set; } = null!;

        public DateTimeOffset CapturedAt { get; set; }
    }
}
