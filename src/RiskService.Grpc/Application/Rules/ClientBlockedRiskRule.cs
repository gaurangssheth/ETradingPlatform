namespace RiskService.Grpc.Application.Rules
{
    public sealed class ClientBlockedRiskRule : IRiskRule
    {
        private static readonly HashSet<string> BlockedClients = new(StringComparer.OrdinalIgnoreCase)
        {
            "blocked-client-001"
        };

        public RiskRuleResult Check(RiskCheckRequestModel request)
        {
            if (BlockedClients.Contains(request.ClientId))
            {
                return RiskRuleResult.Fail(RiskReasonCodes.ClientBlocked, $"Client {request.ClientId} is blocked.");
            }

            return RiskRuleResult.Pass();
        }
    }
}
