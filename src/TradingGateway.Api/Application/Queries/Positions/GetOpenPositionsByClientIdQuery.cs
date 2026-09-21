namespace TradingGateway.Api.Application.Queries.Positions
{
    public sealed record GetOpenPositionsByClientIdQuery(string ClientId, string? CorrelationId);
}
