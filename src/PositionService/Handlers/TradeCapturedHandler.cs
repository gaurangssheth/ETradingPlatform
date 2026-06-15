using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PositionService.Infrastructure.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;

namespace PositionService.Handlers
{
    public sealed class TradeCapturedHandler : IHandleMessages<TradeCaptured>
    {
        private readonly ILogger<TradeCapturedHandler> logger;
        private readonly IUnitOfWork unitOfWork;

        public TradeCapturedHandler(IUnitOfWork unitOfWork, ILogger<TradeCapturedHandler> logger)
        {
            this.unitOfWork = unitOfWork;
            this.logger = logger;
        }

        public async Task Handle(TradeCaptured message, IMessageHandlerContext context)
        {
            if (await unitOfWork.ProcessedTrades.ExistsAsync(message.TradeId, context.CancellationToken))
            {
                logger.LogWarning(
                    "TradeCaptured already processed. TradeId={TradeId}, OrderId={OrderId}, CorrelationId={CorrelationId}",
                    message.TradeId,
                    message.OrderId,
                    message.CorrelationId);

                return;
            }

            var signedQuantity = message.Side == OrderSide.Buy ? 
                message.Quantity : -message.Quantity;

            var position = await unitOfWork.Positions.GetByClientAndSymbolAsync(
                message.ClientId, message.Symbol, context.CancellationToken);

            if (position is null)
            {
                position = new Domain.Position
                {
                    Id = Guid.NewGuid(),
                    ClientId = message.ClientId,
                    Symbol = message.Symbol,
                    NetQuantity = signedQuantity,
                    AveragePrice = message.Price,
                    CorrelationId = message.CorrelationId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await unitOfWork.Positions.AddAsync(position, context.CancellationToken);
            }
            else
            {
                position.AveragePrice = CalculateNewAveragePrice(
                    position.NetQuantity,
                    position.AveragePrice, 
                    signedQuantity,
                    message.Price);
                position.NetQuantity += signedQuantity;
                position.CorrelationId = message.CorrelationId;
                position.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await unitOfWork.ProcessedTrades.AddAsync(new Domain.ProcessedTrade
            {
                TradeId = message.TradeId,
                OrderId = message.OrderId,
                ClientId = message.ClientId,
                Symbol = message.Symbol,
                CorrelationId = message.CorrelationId,
                ProcessedAt = DateTimeOffset.UtcNow
            }, context.CancellationToken);

            try
            {
                await unitOfWork.SaveChangesAsync(context.CancellationToken);
            }
            catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
            {
                logger.LogWarning(
                    "TradeCaptured duplicate detected during save. TradeId={TradeId}, OrderId={OrderId}, CorrelationId={CorrelationId}",
                    message.TradeId,
                    message.OrderId,
                    message.CorrelationId);

                return;
            }

            logger.LogInformation(
                "Position updated. PositionId={PositionId}, ClientId={ClientId}, Symbol={Symbol}, NetQuantity={NetQuantity}, CorrelationId={CorrelationId}",
                position.Id,
                position.ClientId,
                position.Symbol,
                position.NetQuantity,
                position.CorrelationId);

            await context.Publish(new PositionUpdated
            {
                PositionId = position.Id,
                ClientId = position.ClientId,
                Symbol = position.Symbol,
                NetQuantity = position.NetQuantity,
                AveragePrice = position.AveragePrice,
                CorrelationId = position.CorrelationId
            });
        }

        private decimal CalculateNewAveragePrice(
            decimal existingNetQuantity,
            decimal existingAveragePrice,
            decimal newSignedQuantity,
            decimal newPrice)
        {
            var newNetQuantity = existingNetQuantity + newSignedQuantity;

            if (newNetQuantity == 0)
            {
                return 0m;
            }

            if (existingNetQuantity == 0)
            {
                return newPrice;
            }

            var existingValue = existingNetQuantity * existingAveragePrice;
            var newValue = newSignedQuantity * newPrice;

            return Math.Abs((existingValue + newValue) / newNetQuantity);
        }

        private static bool IsDuplicateKeyException(DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException
                && (sqlException.Number == 2627 || sqlException.Number == 2601);
        }
    }
}
