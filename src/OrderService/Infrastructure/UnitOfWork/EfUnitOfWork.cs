using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Repositories;

namespace OrderService.Infrastructure.UnitOfWork;

public class EfUnitOfWork : IUnitOfWork, IDisposable
{
    private readonly OrderDbContext dbContext;

    public EfUnitOfWork(OrderDbContext dbContext, IOrderRepository orderRepository)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        Orders = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public IOrderRepository Orders { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);

    public void Dispose()
    {
        dbContext.Dispose();
    }
}
