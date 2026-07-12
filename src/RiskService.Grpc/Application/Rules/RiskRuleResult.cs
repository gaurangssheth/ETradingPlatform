namespace RiskService.Grpc.Application.Rules
{
    public sealed class RiskRuleResult
    {
        public bool Passed { get; init; }

        public string ReasonCode { get; init; } = null!;

        public string Reason { get; init; } = null!;

        public static RiskRuleResult Pass()
        {
            return new RiskRuleResult
            {
                Passed = true,
                ReasonCode = RiskReasonCodes.Approved,
                Reason = "Rule passed."
            };
        }

        public static RiskRuleResult Fail(string reasonCode, string reason)
        {
            return new RiskRuleResult
            {
                Passed = false,
                ReasonCode = reasonCode,
                Reason = reason
            };
        }
    }
}