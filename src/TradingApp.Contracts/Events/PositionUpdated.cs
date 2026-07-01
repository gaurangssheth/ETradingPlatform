using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;

namespace TradingApp.Contracts.Events
{
    public class PositionUpdated : IEvent, ICorrelatedMessage
    {
        public Guid PositionId { get; set; }

        public string ClientId { get; set; } = null!;

        public string Symbol { get; set; } = null!;

        public decimal NetQuantity { get; set; }

        public decimal AveragePrice { get; set; }

        public decimal RealisedPnl { get; set; }

        public decimal UnrealisedPnl { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public string CorrelationId { get; set; } = null!;
    }
}
