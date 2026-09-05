using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.PositionAccounting
{
    public class MarkPriceSelector
    {
        public decimal GetMarkPrice(decimal netQuantity, decimal bid, decimal ask)
        {
            if (netQuantity > 0)
            {
                return bid;
            }
            else if (netQuantity < 0)
            {
                return ask;
            }

            throw new InvalidOperationException("Cannot select a mark price for a flat position.");
        }
    }
}
