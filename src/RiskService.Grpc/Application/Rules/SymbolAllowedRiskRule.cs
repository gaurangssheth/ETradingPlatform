namespace RiskService.Grpc.Application.Rules
{
    public sealed class SymbolAllowedRiskRule : IRiskRule
    {
        private static readonly HashSet<string> AllowedSymbols = new(StringComparer.OrdinalIgnoreCase)
        {
            "EURUSD",
            "GBPUSD",
            "USDJPY"
        };

        public RiskRuleResult Check(RiskCheckRequestModel request)
        {
            if (!AllowedSymbols.Contains(request.Symbol))
            {
                return RiskRuleResult.Fail(
                    RiskReasonCodes.SymbolNotAllowed,
                    $"Symbol {request.Symbol} is not allowed.");
            }

            return RiskRuleResult.Pass();
        }
    }
}
