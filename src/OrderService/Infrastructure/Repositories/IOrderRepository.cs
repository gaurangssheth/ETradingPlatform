using OrderService.Domain;

namespace OrderService.Infrastructure.Repositories;

public interface IOrderRepository : IGenericRepository<Order>
{
    //Task AddAsync(Order order, CancellationToken cancellationToken = default);
}
