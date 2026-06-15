using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Domain;
using TradeCaptureService.Infrastructure.UnitOfWork;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;

namespace TradeCaptureService.Handlers
{
    public class OrderAcceptedHandler : IHandleMessages<OrderAccepted>
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly ILogger<OrderAcceptedHandler> logger;

        public OrderAcceptedHandler(IUnitOfWork unitOfWork, ILogger<OrderAcceptedHandler> logger)
        {
            this.unitOfWork = unitOfWork;
            this.logger = logger;
        }

        public async Task Handle(OrderAccepted message, IMessageHandlerContext context)
        {
            if (await unitOfWork.Trades.ExistsForOrderAsync(message.OrderId, context.CancellationToken)) {
                logger.LogWarning("Trade already exists for OrderId={OrderId}. Skipping duplicate. CorrelationId={CorrelationId}", 
                    message.OrderId,
                    message.CorrelationId);
                return;
            }

            var tradeId = Guid.NewGuid();
            var capturedAt = DateTimeOffset.UtcNow;

            var trade = new Trade
            {
                Id = tradeId,
                OrderId = message.OrderId,
                ClientId = message.ClientId,
                Symbol = message.Symbol,
                Side = message.Side,
                OrderType = message.OrderType,
                Quantity = message.Quantity,
                Price = message.Price,
                Notional = message.Notional,
                Status = TradeStatus.Captured,
                CapturedAt = capturedAt,
                CorrelationId = message.CorrelationId
            };

            await unitOfWork.Trades.AddAsync(trade, context.CancellationToken);
            await unitOfWork.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation(
                "Trade captured. TradeId={TradeId}, OrderId={OrderId}, Symbol={Symbol}, CorrelationId={CorrelationId}",
                trade.Id,
                trade.OrderId,
                trade.Symbol,
                trade.CorrelationId);

            await context.Publish(new TradeCaptured
            {
                TradeId = trade.Id,
                OrderId = trade.OrderId,
                ClientId = trade.ClientId,
                Symbol = trade.Symbol,
                Side = trade.Side,
                Quantity = trade.Quantity,
                Price = trade.Price,
                Notional = trade.Notional,
                Status = trade.Status,
                CapturedAt = trade.CapturedAt,
                CorrelationId = trade.CorrelationId
            });

        }
    }
}
