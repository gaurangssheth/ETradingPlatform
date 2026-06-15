using PositionService.Infrastructure.Repositories;

namespace PositionService.Infrastructure.UnitOfWork;

public interface IUnitOfWork
{
    IPositionRepository Positions { get; }
    IProcessedTradeRepository ProcessedTrades { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
