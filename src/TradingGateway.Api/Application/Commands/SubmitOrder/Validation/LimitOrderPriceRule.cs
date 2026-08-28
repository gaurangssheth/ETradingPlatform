using TradingApp.Contracts.Shared;
using TradingApp.Shared.Validation;

namespace TradingGateway.Api.Application.Commands.SubmitOrder.Validation
{
    public sealed class LimitOrderPriceRule : IValidationRule<(string? OrderType, decimal? LimitPrice)>
    {
        public string? Validate((string? OrderType, decimal? LimitPrice) value)
        {
            if (!Enum.TryParse<OrderType>(value.OrderType, true, out var orderType))
            {
                return null;
            }

            if (orderType != OrderType.Limit)
            {
                return null;
            }

            if (!value.LimitPrice.HasValue)
            {
                return "LimitPrice is required for Limit orders.";
            }

            if (value.LimitPrice.Value <= 0)
            {
                return "LimitPrice must be greater than 0 for Limit orders.";
            }

            return null;
        }
    }
}
