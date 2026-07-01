using OrderService.Domain;
using OrderService.Infrastructure.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;
using TradingApp.Contracts.Events;

namespace OrderService.Handlers
{
    public class SubmitOrderHandler : IHandleMessages<SubmitOrder>
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly ILogger<SubmitOrderHandler> logger;

        public SubmitOrderHandler(IUnitOfWork unitOfWork, ILogger<SubmitOrderHandler> logger)
        {
            this.unitOfWork = unitOfWork;
            this.logger = logger;
        }

        public async Task Handle(SubmitOrder message, IMessageHandlerContext context)
        {
            var accepted = DateTimeOffset.UtcNow;

            var order = new Order
            {
                Id = message.OrderId,
                ClientId = message.ClientId,
                Symbol = message.Symbol,
                Side = message.Side,
                OrderType = message.OrderType,
                Quantity = message.Quantity,
                Status = "Accepted",
                CorrelationId = message.CorrelationId,
                CreatedAt = accepted,
                AcceptedAt = accepted
            };

            await unitOfWork.Orders.AddAsync(order, context.CancellationToken);
            await unitOfWork.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation(
            "Order saved. OrderId={OrderId}, ClientId={ClientId}, Symbol={Symbol}, CorrelationId={CorrelationId}",
            order.Id,
            order.ClientId,
            order.Symbol,
            order.CorrelationId);

            await context.Publish(new OrderAccepted
            {
                OrderId = order.Id,
                ClientId = order.ClientId,
                Symbol = order.Symbol,
                Side = order.Side,
                Quantity = order.Quantity,
                OrderType = order.OrderType,
                AcceptedAt = order.AcceptedAt!.Value,
                CorrelationId = message.CorrelationId
            });
        }
    }
}
