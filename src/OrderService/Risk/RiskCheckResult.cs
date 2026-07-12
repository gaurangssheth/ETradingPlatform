using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderService.Risk
{
    public sealed class RiskCheckResult
    {
        public bool Approved { get; init; }

        public string ReasonCode { get; init; } = null!;

        public string Reason { get; init; } = null!;

        public Guid RiskDecisionId { get; init; }
    }
}
