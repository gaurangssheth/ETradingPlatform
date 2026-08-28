using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Application;
using TradeCaptureService.Pricing;
using TradeCaptureService.Services;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;

namespace TradeCaptureService.Handlers
{
    public class OrderAcceptedHandler : IHandleMessages<OrderAccepted>
    {
        private readonly IPricingClient pricingClient;
        private readonly ExecutionPriceCalculator executionPriceCalculator;
        private readonly TradeCaptureProcessor tradeCaptureProcessor;
        private readonly ILogger<OrderAcceptedHandler> logger;

        public OrderAcceptedHandler(
            IPricingClient pricingClient,
            ExecutionPriceCalculator executionPriceCalculator,
            TradeCaptureProcessor tradeCaptureProcessor,
            ILogger<OrderAcceptedHandler> logger)
        {
            this.pricingClient = pricingClient;
            this.executionPriceCalculator = executionPriceCalculator;
            this.tradeCaptureProcessor = tradeCaptureProcessor;
            this.logger = logger;
        }

        public async Task Handle(OrderAccepted message, IMessageHandlerContext context)
        {
            if (message.OrderType == OrderType.Limit)
            {
                return;
            }

            if (await this.tradeCaptureProcessor.TradeExistsForOrderAsync(message.OrderId, context.CancellationToken))
            {
                logger.LogWarning("Trade already exists for OrderId={OrderId}. Skipping duplicate. CorrelationId={CorrelationId}",
                    message.OrderId,
                    message.CorrelationId);
                return;
            }

            var quote = await this.pricingClient.GetPriceAsync(message.Symbol,
                message.CorrelationId,
                context.CancellationToken);

            var executionPrice = executionPriceCalculator.GetExecutionPrice(message.Side, quote);

            var riskDesicionId = message.RiskDecisionId
                ?? throw new InvalidOperationException(
                $"RiskDecisionId is required for accepted order {message.OrderId}.");

            var request = new TradeCaptureRequest
            {
                OrderId = message.OrderId,
                ClientId = message.ClientId,
                Symbol = message.Symbol,
                Side = message.Side,
                OrderType = message.OrderType,
                Quantity = message.Quantity,
                ExecutionPrice = executionPrice,
                RiskDecisionId = riskDesicionId,
                ExecutedAt = DateTimeOffset.UtcNow,
                CorrelationId = message.CorrelationId
            };

            await this.tradeCaptureProcessor.CaptureAsync(
                request,
                context);


        }
    }
}
