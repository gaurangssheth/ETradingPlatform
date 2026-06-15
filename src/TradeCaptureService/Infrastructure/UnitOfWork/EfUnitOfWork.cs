using Microsoft.EntityFrameworkCore;
using TradeCaptureService.Infrastructure.Persistence;
using TradeCaptureService.Infrastructure.Repositories;

namespace TradeCaptureService.Infrastructure.UnitOfWork;

public class EfUnitOfWork : IUnitOfWork, IDisposable
{
    private readonly TradeDbContext dbContext;

    public EfUnitOfWork(TradeDbContext dbContext, ITradeRepository tradeRepository)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.Trades = tradeRepository ?? throw new ArgumentNullException(nameof(tradeRepository));
    }

    public ITradeRepository Trades { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);

    public void Dispose()
    {
        this.dbContext.Dispose();
    }
}
