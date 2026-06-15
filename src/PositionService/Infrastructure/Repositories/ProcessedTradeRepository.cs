using Microsoft.EntityFrameworkCore;
using PositionService.Domain;
using PositionService.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Infrastructure.Repositories
{
    public class ProcessedTradeRepository: GenericRepository<ProcessedTrade>, IProcessedTradeRepository
    {
        private readonly ILogger<ProcessedTradeRepository> logger;
        public ProcessedTradeRepository(PositionDbContext dbContext, ILogger<ProcessedTradeRepository> logger): base(dbContext)
        {
            this.logger = logger;
        }

        public async Task<bool> ExistsAsync(Guid tradeId, CancellationToken cancellationToken = default)
        {
            return await context.ProcessedTrades.AnyAsync(x => x.TradeId == tradeId, cancellationToken);
        }
    }
}
