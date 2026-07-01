using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingApp.Contracts.Shared
{
    public interface ICorrelatedMessage
    {
        string CorrelationId { get; set; }
    }
}
