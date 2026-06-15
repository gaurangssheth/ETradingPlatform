using TradeCaptureService.Infrastructure.Repositories;

namespace TradeCaptureService.Infrastructure.UnitOfWork;

public interface IUnitOfWork
{
    public ITradeRepository Trades { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
