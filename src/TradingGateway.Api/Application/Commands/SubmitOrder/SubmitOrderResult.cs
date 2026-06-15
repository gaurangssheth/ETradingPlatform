namespace TradingGateway.Api.Application.Commands.SubmitOrder
{
    public sealed class SubmitOrderResult
    {
        public Guid OrderId { get; init; }

        public bool Accepted { get; init; }

        public string Status { get; init; } = null!;

        public string? Error { get; init; }

        public string CorrelationId { get; init; } = null!;
    }
}
