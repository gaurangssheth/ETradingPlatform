using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;

namespace TradeCaptureService.Application
{
    public sealed class TradeCaptureRequest
    {
        public Guid OrderId { get; init; }

        public string ClientId { get; init; } = string.Empty;

        public string Symbol { get; init; } = string.Empty;

        public OrderSide Side { get; init; }

        public OrderType OrderType { get; init; }

        public decimal Quantity { get; init; }

        public decimal ExecutionPrice { get; init; }

        public string RiskDecisionId { get; init; } = string.Empty;

        public DateTimeOffset ExecutedAt { get; init; }

        public string CorrelationId { get; init; } = string.Empty;
    }
}
