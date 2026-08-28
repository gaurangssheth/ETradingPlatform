using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;
using TradingApp.SharedKernel;

namespace TradingApp.Contracts.Commands
{
    public sealed class StartLimitOrder : ICommand, ICorrelatedMessage
    {
        public Guid OrderId { get; set; }

        public string ClientId { get; set; } = string.Empty;

        public string Symbol { get; set; } = string.Empty;

        public OrderSide Side { get; set; }

        public decimal Quantity { get; set; }

        public decimal LimitPrice { get; set; }

        public string? RiskDecisionId { get; set; } = null!;

        public string CorrelationId { get; set; } = string.Empty;
    }
}
