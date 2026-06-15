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
    public class PositionRepository : GenericRepository<Position>, IPositionRepository
    {
        private readonly ILogger<PositionRepository> logger;
        public PositionRepository(PositionDbContext dbContext, ILogger<PositionRepository> logger) : base(dbContext)
        {
            this.logger = logger;
        }
        public override async Task<Position> UpsertAsync(Position position, CancellationToken cancellationToken = default)
        {
            try
            {
                var existing = await GetByIdAsync(position.Id, cancellationToken);
                if (existing is null)
                {
                    return await AddAsync(position, cancellationToken);
                }
                context.Entry(existing).CurrentValues.SetValues(position);
                return existing;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while upserting the position with ID {PositionId}", position.Id);
                return null;
            }
        }
        public override async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                var existing = await GetByIdAsync(id, cancellationToken);
                if (existing is null)
                {
                    return false;
                }
                context.Positions.Remove(existing);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while deleting the position with ID {id}", id);
                return false;
            }
        }

        public async Task<Position?> GetByClientAndSymbolAsync(string clientId, string symbol, CancellationToken cancellationToken = default)
        {
            return await context.Positions.SingleOrDefaultAsync(p => p.ClientId == clientId && p.Symbol == symbol, cancellationToken);
        }
    }
}
