using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Application;
using TradingApp.Contracts.Commands;
using TradingApp.Contracts.Shared;

namespace TradeCaptureService.Handlers
{
    public sealed class ExecuteLimitOrderHandler : IHandleMessages<ExecuteLimitOrder>
    {
        private readonly TradeCaptureProcessor tradeCaptureProcessor;
        private readonly ILogger<ExecuteLimitOrderHandler> logger;

        public ExecuteLimitOrderHandler(
            TradeCaptureProcessor tradeCaptureProcessor,
            ILogger<ExecuteLimitOrderHandler> logger)
        {
            this.tradeCaptureProcessor = tradeCaptureProcessor;
            this.logger = logger;
        }

        public async Task Handle(ExecuteLimitOrder message, IMessageHandlerContext context)
        {
            var request = new TradeCaptureRequest
            {
                OrderId = message.OrderId,
                ClientId = message.ClientId,
                Symbol = message.Symbol,
                Side = message.Side,
                OrderType = OrderType.Limit,
                Quantity = message.Quantity,
                ExecutionPrice = message.ExecutionPrice,
                RiskDecisionId = message.RiskDecisionId,
                ExecutedAt = message.ExecutedAt,
                CorrelationId = message.CorrelationId
            };

            await this.tradeCaptureProcessor.CaptureAsync(request,
                context);

            this.logger.LogInformation(
                "Limit order execution processed. OrderId={OrderId}, Symbol={Symbol}, ExecutionPrice={ExecutionPrice}, CorrelationId={CorrelationId}",
                message.OrderId,
                message.Symbol,
                message.ExecutionPrice,
                message.CorrelationId);
        }
    }
}
