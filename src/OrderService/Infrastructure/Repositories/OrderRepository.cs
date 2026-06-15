using OrderService.Domain;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.Repositories;

public class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    private ILogger<OrderRepository> logger;
    public OrderRepository(OrderDbContext dbContext, ILogger<OrderRepository> logger) : base(dbContext) {
        this.logger = logger;                
    }

    //public Task AddAsync(Order order, CancellationToken cancellationToken = default) =>
    //    dbContext.Orders.AddAsync(order, cancellationToken).AsTask();

    public override async Task<Order> UpsertAsync(Order order, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await GetByIdAsync(order.Id, cancellationToken);
            if (existing is null)
            {
                return await AddAsync(order, cancellationToken);
            }
            context.Entry(existing).CurrentValues.SetValues(order);
            return existing;
        }
        catch (Exception ex)
        {
            // Log the exception here using your logging framework
            logger.LogError(ex, "An error occurred while upserting the order with ID {OrderId}", order.Id);
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
            context.Orders.Remove(existing);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while deleting the order with ID {id}", id);
            return false;
        }
    }
}
