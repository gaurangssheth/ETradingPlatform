using PositionService.Infrastructure.Persistence;
using PositionService.Infrastructure.Repositories;

namespace PositionService.Infrastructure.UnitOfWork;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly PositionDbContext dbContext;

    public EfUnitOfWork(PositionDbContext dbContext, 
        IPositionRepository positionRepository, 
        IProcessedTradeRepository processedTrades)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        Positions = positionRepository ?? throw new ArgumentNullException(nameof(positionRepository));
        ProcessedTrades = processedTrades;
    }

    public IPositionRepository Positions { get; private set; }

    public IProcessedTradeRepository ProcessedTrades { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
