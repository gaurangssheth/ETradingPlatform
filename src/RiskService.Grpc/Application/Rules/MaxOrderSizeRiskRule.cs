namespace RiskService.Grpc.Application.Rules
{
    public sealed class MaxOrderSizeRiskRule : IRiskRule
    {
        private const decimal MaxQuantity = 1_000_000m;

        public RiskRuleResult Check(RiskCheckRequestModel request)
        {
            if (request.Quantity <= 0)
            {
                return RiskRuleResult.Fail(
                    RiskReasonCodes.InvalidQuantity,
                    "Quantity must be greater than zero.");
            }

            if (request.Quantity > MaxQuantity)
            {
                return RiskRuleResult.Fail(
                    RiskReasonCodes.MaxOrderSizeExceeded,
                    $"Quantity {request.Quantity} exceeds maximum order size {MaxQuantity}.");
            }

            return RiskRuleResult.Pass();
        }
    }
}
