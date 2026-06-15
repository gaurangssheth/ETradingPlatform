using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Domain
{
    public class ProcessedTrade
    {
        public Guid TradeId { get; set; }

        public Guid OrderId { get; set; }

        public string ClientId { get; set; } = null!;

        public string Symbol { get; set; } = null!;

        public string CorrelationId { get; set; } = null!;

        public DateTimeOffset ProcessedAt { get; set; }
    }
}
