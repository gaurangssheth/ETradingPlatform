using TradingApp.Contracts.Shared;

namespace RiskService.Grpc.Application
{
    public sealed record RiskCheckRequestModel
    {
        public Guid OrderId { get; init; }

        public string ClientId { get; init; } = null!;

        public string Symbol { get; init; } = null!;

        public OrderSide Side { get; init; }

        public decimal Quantity { get; init; }

        public OrderType OrderType { get; init; }
    }
}
