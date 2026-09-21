using Azure;
using Grpc.Core;
using System.Globalization;
using TradingApp.Shared.Messaging.Correlation;
using TradingGateway.Api.ClientModels;

namespace TradingGateway.Api.Clients
{
    public class GrpcPositionServiceClient : IPositionServiceClient
    {
        private readonly PositionService.Grpc.PositionService.PositionServiceClient positionServiceClient;
        private readonly ILogger<GrpcPositionServiceClient> logger;

        public GrpcPositionServiceClient(PositionService.Grpc.PositionService.PositionServiceClient positionServiceClient,
            ILogger<GrpcPositionServiceClient> logger)
        {
            this.positionServiceClient = positionServiceClient;
            this.logger = logger;
        }
        public async Task<IReadOnlyList<PositionSummaryResponse>> GetOpenPositionsByClientIdAsync(
            string clientId,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var headers = new Metadata();

                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    headers.Add(GrpcCorrelationConstants.MetadataKey, correlationId);
                }

                var response = await positionServiceClient.GetOpenPositionsByClientIdAsync(
                    new PositionService.Grpc.GetOpenPositionsByClientIdRequest { ClientId = clientId },
                    headers: headers,
                    cancellationToken: cancellationToken);

                return response.Positions
                .Select(position =>
                    new PositionSummaryResponse(
                        InstrumentId: Guid.Parse(position.InstrumentId),
                        Symbol: position.Symbol,
                        AssetClass: position.AssetClass,
                        NetQuantity: decimal.Parse(
                            position.NetQuantity,
                            CultureInfo.InvariantCulture),
                        decimal.Parse(
                            position.AveragePrice,
                            CultureInfo.InvariantCulture),
                        PnlCurrency: position.PnlCurrency,
                        RealisedPnl: decimal.Parse(
                            position.RealisedPnl,
                            CultureInfo.InvariantCulture),
                        UnrealisedPnl: decimal.Parse(
                            position.UnrealisedPnl,
                            CultureInfo.InvariantCulture),
                        TotalPnl: decimal.Parse(
                            position.TotalPnl,
                            CultureInfo.InvariantCulture)))
                .ToList();
            }
            catch (RpcException ex)
            {
                logger.LogError(ex,
                    "Failed to retrieve open  positions from PositionService.");

                throw;
            }
        }
    }
}
