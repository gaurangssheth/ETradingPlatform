using OrderService.Infrastructure.Repositories;

namespace OrderService.Infrastructure.UnitOfWork;

public interface IUnitOfWork
{
    IOrderRepository Orders { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
