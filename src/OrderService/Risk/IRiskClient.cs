using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;

namespace OrderService.Risk
{
    public interface IRiskClient
    {
        Task<RiskCheckResult> CheckOrderRiskAsync(SubmitOrder order, 
            CancellationToken cancellationToken = default);
    }
}
