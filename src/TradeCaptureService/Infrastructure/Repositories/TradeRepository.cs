using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Domain;
using TradeCaptureService.Infrastructure.Persistence;
using TradeCaptureService.Repositories;

namespace TradeCaptureService.Infrastructure.Repositories
{
    public class TradeRepository : GenericRepository<Trade>, ITradeRepository
    {
        private readonly ILogger<TradeRepository> _logger;

        public TradeRepository(TradeDbContext dbContext, ILogger<TradeRepository> logger): base(dbContext)
        {
            this._logger = logger;
        }

        public async Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            return await context.Trades.AnyAsync(x => x.OrderId == orderId, cancellationToken);
        }

        public override async Task<Trade> UpsertAsync(Trade trade, CancellationToken cancellationToken = default)
        {
            try
            {
                var existing = await GetByIdAsync(trade.Id, cancellationToken);
                if (existing is null)
                {
                    return await AddAsync(trade, cancellationToken);
                }
                this.context.Entry(existing).CurrentValues.SetValues(trade);
                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while upserting the trade with ID {TradeId}", trade.Id);
                return null;
            }
        }
    }
}