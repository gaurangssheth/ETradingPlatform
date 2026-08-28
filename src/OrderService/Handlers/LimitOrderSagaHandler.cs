using OrderService.Domain;
using OrderService.Infrastructure.UnitOfWork;
using OrderService.Pricing;
using OrderService.Sagas;
using OrderService.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;

namespace OrderService.Handlers
{
    public sealed class LimitOrderSagaHandler : 
        Saga<LimitOrderSagaData>, 
        IAmStartedByMessages<StartLimitOrder>,
        IHandleTimeouts<CheckOrderLimitPrice>,
        IHandleMessages<TradeCaptured>
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IPricingClient pricingClient;
        private readonly LimitOrderExecutionEvaluator executionEvaluator;
        private readonly ILogger<LimitOrderSagaHandler> logger;

        public LimitOrderSagaHandler(
            IUnitOfWork unitOfWork,
            IPricingClient pricingClient,
            LimitOrderExecutionEvaluator executionEvaluator,
            ILogger<LimitOrderSagaHandler> logger)
        {
            this.unitOfWork = unitOfWork;
            this.pricingClient = pricingClient;
            this.executionEvaluator = executionEvaluator;
            this.logger = logger;
        }

        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<LimitOrderSagaData> mapper)
        {
            mapper.MapSaga(saga => saga.OrderId)
                .ToMessage<StartLimitOrder>(message => message.OrderId)
                .ToMessage<TradeCaptured>(message => message.OrderId);
        }
        public async Task Handle(StartLimitOrder message, IMessageHandlerContext context)
        {
            Data.OrderId = message.OrderId;
            Data.ClientId = message.ClientId;
            Data.Symbol = message.Symbol;
            Data.Side = message.Side;
            Data.Quantity = message.Quantity;
            Data.LimitPrice = message.LimitPrice;
            Data.Status = LimitOrderSagaStatus.Working;
            Data.RiskDecisionId = message.RiskDecisionId;
            Data.CorrelationId = message.CorrelationId;

            var order = await this.unitOfWork.Orders.GetByIdAsync(
                message.OrderId,
                context.CancellationToken);

            order.Status = OrderStatus.Working;

            await this.unitOfWork.SaveChangesAsync(context.CancellationToken);

            if (order is null)
            {
                throw new InvalidOperationException(
                    $"Order {message.OrderId} was not found.");
            }

            logger.LogInformation(
                "Limit order started. OrderId={OrderId}, ClientId={ClientId}, Symbol={Symbol}, Side={Side}, Quantity={Quantity}, LimitPrice={LimitPrice}, RiskDecisionId={RiskDecisionId}, CorrelationId={CorrelationId}",
                Data.OrderId,
                Data.ClientId,
                Data.Symbol,
                Data.Side,
                Data.Quantity,
                Data.LimitPrice,
                Data.RiskDecisionId,
                Data.CorrelationId);

            await RequestTimeout<CheckOrderLimitPrice>(
                context,
                TimeSpan.FromSeconds(1));
        }
        
        public async Task Timeout(CheckOrderLimitPrice state, IMessageHandlerContext context)
        {
            var quote = await pricingClient.GetPriceAsync(
                Data.Symbol, Data.CorrelationId, context.CancellationToken);

            var canExecute = executionEvaluator.CanExecute(
                Data.Side, Data.LimitPrice, quote.Bid,
                quote.Ask);

            if (!canExecute)
            {
                await RequestTimeout<CheckOrderLimitPrice>(
                    context,
                    TimeSpan.FromSeconds(1));

                logger.LogDebug(
                    "Limit order still working. OrderId={OrderId}, Bid={Bid}, Ask={Ask}, LimitPrice={LimitPrice}",
                    Data.OrderId,
                    quote.Bid,
                    quote.Ask,
                    Data.LimitPrice);

                return;
            }

            var executionPrice = Data.Side == OrderSide.Buy
                ? quote.Ask
                : quote.Bid;

            var order = await this.unitOfWork.Orders.GetByIdAsync(
                Data.OrderId,
                context.CancellationToken);

            if (order is null)
            {
                throw new InvalidOperationException(
                    $"Order {Data.OrderId} was not found.");
            }

            order.Status = OrderStatus.Triggered;

            await this.unitOfWork.SaveChangesAsync(
                context.CancellationToken);

            await context.Send(new ExecuteLimitOrder
            {
                OrderId = Data.OrderId,
                ClientId = Data.ClientId,
                Symbol = Data.Symbol,
                Side = Data.Side,
                Quantity = Data.Quantity,
                LimitPrice = Data.LimitPrice,
                ExecutionPrice = executionPrice,
                ExecutedAt = DateTimeOffset.UtcNow,
                RiskDecisionId = Data.RiskDecisionId,
                CorrelationId = Data.CorrelationId
            });

            logger.LogInformation(
                "Limit order triggered. OrderId={OrderId}, Symbol={Symbol}, LimitPrice={LimitPrice}, Bid={Bid}, Ask={Ask}, RiskDecisionId={RiskDecisionId}, CorrelationId={CorrelationId}",
                Data.OrderId,
                Data.Symbol,
                Data.LimitPrice,
                quote.Bid,
                quote.Ask,
                Data.RiskDecisionId,
                Data.CorrelationId);

            Data.Status = LimitOrderSagaStatus.Triggered;
        }

        public async Task Handle(TradeCaptured message, IMessageHandlerContext context)
        {
            var order = await this.unitOfWork.Orders.GetByIdAsync(
                Data.OrderId,
                context.CancellationToken);

            if (order is null)
            {
                throw new InvalidOperationException(
                    $"Order {Data.OrderId} was not found.");
            }

            order.Status = OrderStatus.Filled;

            await this.unitOfWork.SaveChangesAsync(
                context.CancellationToken);

            Data.Status = LimitOrderSagaStatus.Filled;

            logger.LogInformation(
                "Limit order filled. OrderId={OrderId}, TradeId={TradeId}, Symbol={Symbol}, CorrelationId={CorrelationId}",
                Data.OrderId,
                message.TradeId,
                Data.Symbol,
                Data.CorrelationId);

            MarkAsComplete();
        }
    }
}
