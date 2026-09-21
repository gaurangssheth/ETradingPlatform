using TradingGateway.Api.ClientModels;

namespace TradingGateway.Api.Clients
{
    public interface IPositionServiceClient
    {
        Task<IReadOnlyList<PositionSummaryResponse>> GetOpenPositionsByClientIdAsync(
            string clientId,
            string? correlationId = null,
            CancellationToken cancellationToken = default);
    }
}
