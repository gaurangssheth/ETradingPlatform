namespace TradingGateway.Api.Application.Commands.SubmitOrder
{
    public sealed record SubmitOrderCommand(
        string? ClientId,
        string? Symbol,
        string? Side,
        decimal? Quantity,
        string? OrderType,
        string? CorrelationId);
}
