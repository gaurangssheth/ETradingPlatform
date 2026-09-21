using TradingGateway.Api.Application.Queries;
using TradingGateway.Api.ClientModels;
using TradingGateway.Api.Clients;

namespace TradingGateway.Api.Application.Queries.Positions
{
    public sealed class GetOpenPositionsByClientIdQueryHandler : IQueryHandler<GetOpenPositionsByClientIdQuery, IReadOnlyList<PositionSummaryResponse>>
    {
        private readonly IPositionServiceClient positionServiceClient;

        public GetOpenPositionsByClientIdQueryHandler(IPositionServiceClient positionServiceClient)
        {
            this.positionServiceClient = positionServiceClient;
        }

        public Task<IReadOnlyList<PositionSummaryResponse>> HandleAsync(GetOpenPositionsByClientIdQuery query, CancellationToken cancellationToken)
        {
            return positionServiceClient.GetOpenPositionsByClientIdAsync(
                query.ClientId,
                query.CorrelationId,
                cancellationToken
            );
        }
    }
}
