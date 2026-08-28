using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderService.Domain
{
    public enum OrderStatus
    {
        PendingRisk,
        Accepted,
        Working,
        Triggered,
        Filled,
        Rejected
    }
}
