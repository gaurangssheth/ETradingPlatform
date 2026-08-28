using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Calculations;
using TradeCaptureService.Domain;
using TradeCaptureService.Infrastructure.UnitOfWork;
using TradeCaptureService.ReferenceData;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;

namespace TradeCaptureService.Application
{
    public sealed class TradeCaptureProcessor
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IReferenceDataClient referenceDataClient;
        private readonly NotionalCalculatorResolver notionalCalculatorResolver;
        private readonly ILogger<TradeCaptureProcessor> logger;

        public TradeCaptureProcessor(
            IUnitOfWork unitOfWork,
            IReferenceDataClient referenceDataClient,
            NotionalCalculatorResolver notionalCalculatorResolver,
            ILogger<TradeCaptureProcessor> logger)
        {
            this.unitOfWork = unitOfWork;
            this.referenceDataClient = referenceDataClient;
            this.notionalCalculatorResolver = notionalCalculatorResolver;
            this.logger = logger;
        }

        public Task<bool> TradeExistsForOrderAsync(Guid orderId,
            CancellationToken cancellationToken)
        {
            return this.unitOfWork.Trades.ExistsForOrderAsync(
                orderId,
                cancellationToken);
        }

        public async Task CaptureAsync(TradeCaptureRequest message, IMessageHandlerContext context)
        {
            if (await unitOfWork.Trades.ExistsForOrderAsync(message.OrderId, context.CancellationToken))
            {
                logger.LogWarning("Trade already exists for OrderId={OrderId}. Skipping duplicate. CorrelationId={CorrelationId}",
                    message.OrderId,
                    message.CorrelationId);
                return;
            }

            var instrumentReferenceDefinition = await this.referenceDataClient.GetInstrumentAsync(message.Symbol,
                message.CorrelationId,
                context.CancellationToken);

            var instrument = instrumentReferenceDefinition.Instrument;

            var notionalCalculator = notionalCalculatorResolver.Resolve(
                instrument.AssetClass);

            var notional = notionalCalculator.Calculate(
                instrumentReferenceDefinition,
                message.Quantity,
                message.ExecutionPrice);

            var tradeId = Guid.NewGuid();
            var capturedAt = DateTimeOffset.UtcNow;

            var trade = new Trade
            {
                Id = tradeId,
                OrderId = message.OrderId,
                ClientId = message.ClientId,
                InstrumentId = instrument.InstrumentId,
                Symbol = instrument.Symbol,
                AssetClass = instrument.AssetClass,
                Side = message.Side,
                OrderType = message.OrderType,
                Quantity = message.Quantity,
                Price = message.ExecutionPrice,
                Notional = notional,
                NotionalCurrency = instrumentReferenceDefinition.Details.NotionalCurrency,
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
                InstrumentId = trade.InstrumentId,
                ClientId = trade.ClientId,
                Symbol = trade.Symbol,
                AssetClass = trade.AssetClass,
                Side = trade.Side,
                Quantity = trade.Quantity,
                Price = trade.Price,
                Notional = trade.Notional,
                NotionalCurrency = trade.NotionalCurrency.Value,
                Status = trade.Status,
                CapturedAt = trade.CapturedAt,
                CorrelationId = trade.CorrelationId,
            });
        }
    }
}
