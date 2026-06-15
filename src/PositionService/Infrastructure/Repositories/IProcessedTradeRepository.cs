using PositionService.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Infrastructure.Repositories
{
    public interface IProcessedTradeRepository : IGenericRepository<ProcessedTrade>
    {
        Task<bool> ExistsAsync(Guid tradeId, CancellationToken cancellationToken = default);
    }
}
