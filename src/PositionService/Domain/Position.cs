using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Domain
{
    public class Position
    {
        public Guid Id { get; set; }
        public string ClientId { get; set; } = null!;
        public string Symbol { get; set; } = null!;
        public decimal NetQuantity { get; set; }
        public decimal AveragePrice { get; set; }
        public string CorrelationId { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
