using TradeCaptureService.Domain;
using TradeCaptureService.Repositories;

namespace TradeCaptureService.Infrastructure.Repositories
{
    public interface ITradeRepository : IGenericRepository<Trade>
    {
        Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    }
}
