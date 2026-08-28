using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;

namespace OrderService.Services
{
    public sealed class LimitOrderExecutionEvaluator
    {
        public bool CanExecute(
            OrderSide side, 
            decimal limitPrice, 
            decimal bid,
            decimal ask)
        {
            return side switch
            {
                OrderSide.Buy => ask <= limitPrice,
                OrderSide.Sell => bid >= limitPrice,
                _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unsupported order side.")
            };
        }
    }
}
