using Microsoft.EntityFrameworkCore;
using OrderService.Domain;
using OrderService.Infrastructure.UnitOfWork;
using OrderService.Risk;
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
        private readonly IRiskClient riskClient;
        private readonly ILogger<SubmitOrderHandler> logger;

        public SubmitOrderHandler(
            IUnitOfWork unitOfWork, 
            IRiskClient riskClient, 
            ILogger<SubmitOrderHandler> logger)
        {
            this.unitOfWork = unitOfWork;
            this.riskClient = riskClient;
            this.logger = logger;
        }

        public async Task Handle(SubmitOrder message, IMessageHandlerContext context)
        {
            var order = await unitOfWork.Orders.GetByIdAsync(
                message.OrderId, context.CancellationToken);

            if (order is null)
            {
                order = new Order
                {
                    Id = message.OrderId,
                    ClientId = message.ClientId,
                    Symbol = message.Symbol,
                    Side = message.Side,
                    OrderType = message.OrderType,
                    Quantity = message.Quantity,
                    Status = "PendingRisk",
                    CorrelationId = message.CorrelationId,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                await unitOfWork.Orders.AddAsync(order, context.CancellationToken);
                await unitOfWork.SaveChangesAsync(context.CancellationToken);

                logger.LogInformation(
                "Order received and saved as PendingRisk. OrderId={OrderId}, ClientId={ClientId}, Symbol={Symbol}, CorrelationId={CorrelationId}",
                order.Id,
                order.ClientId,
                order.Symbol,
                order.CorrelationId);
            }
            

            var riskDecision = await riskClient.CheckOrderRiskAsync(message, 
                context.CancellationToken);

            if (!riskDecision.Approved)
            {
                await RejectOrderAsync(order, riskDecision, context);
                return;
            }

            await AcceptOrderAsync(order, riskDecision, context);
        }

        private async Task AcceptOrderAsync(
            Order order,
            RiskCheckResult riskDecision,
            IMessageHandlerContext context)
        {
            var acceptedAt = DateTimeOffset.UtcNow;

            order.Status = "Accepted";
            order.AcceptedAt = acceptedAt;
            order.RejectedAt = null;
            order.RejectionReason = null;

            await unitOfWork.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation(
                "Order accepted after risk approval. OrderId={OrderId}, ClientId={ClientId}, Symbol={Symbol}, RiskDecisionId={RiskDecisionId}, CorrelationId={CorrelationId}",
                order.Id,
                order.ClientId,
                order.Symbol,
                riskDecision.RiskDecisionId,
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
                RiskDecisionId = riskDecision.RiskDecisionId.ToString(),
                CorrelationId = order.CorrelationId
            });
        }

        private async Task RejectOrderAsync(
            Order order,
            RiskCheckResult riskDecision,
            IMessageHandlerContext context)
        {
            var rejectedAt = DateTimeOffset.UtcNow;

            order.Status = "Rejected";
            order.AcceptedAt = null;
            order.RejectedAt = rejectedAt;
            order.RejectionReason = riskDecision.Reason;

            await unitOfWork.SaveChangesAsync(context.CancellationToken);

            logger.LogWarning(
                "Order rejected by risk. OrderId={OrderId}, ClientId={ClientId}, Symbol={Symbol}, ReasonCode={ReasonCode}, Reason={Reason}, RiskDecisionId={RiskDecisionId}, CorrelationId={CorrelationId}",
                order.Id,
                order.ClientId,
                order.Symbol,
                riskDecision.ReasonCode,
                riskDecision.Reason,
                riskDecision.RiskDecisionId,
                order.CorrelationId);

            await context.Publish(new OrderRejected
            {
                OrderId = order.Id,
                ClientId = order.ClientId,
                Symbol = order.Symbol,
                Side = order.Side,
                Quantity = order.Quantity,
                OrderType = order.OrderType,
                Reason = riskDecision.Reason,
                RejectedAt = order.RejectedAt!.Value,
                RiskDecisionId = riskDecision.RiskDecisionId.ToString(),
                CorrelationId = order.CorrelationId
            });
        }
    }
}
