using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contracts
{
   public class CreateOrderRequest
   {
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;
   }
}
