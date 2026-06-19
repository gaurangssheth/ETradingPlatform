using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.PositionAccounting
{
    public sealed class PositionCalculationResult
    {
        public decimal NewNetQuantity { get; set; }

        public decimal NewAveragePrice { get; set; }

        public decimal RealisedPnl { get; set; }
    }
}
