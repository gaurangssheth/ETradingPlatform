using RiskService.Grpc.Application;
using RiskService.Grpc.Application.Rules;

namespace RiskService.Grpc.Application.Rules
{
    public class ClientActiveRiskRule : IRiskRule
    {
        private static readonly HashSet<string> ActiveClients = new(StringComparer.OrdinalIgnoreCase)
        {
            "client-001",
            "client-002",
            "client-003"
        };
        public RiskRuleResult Check(RiskCheckRequestModel request)
        {
            if (!ActiveClients.Contains(request.ClientId))
            {
                return RiskRuleResult.Fail(
                    RiskReasonCodes.ClientNotActive,
                    $"Client {request.ClientId} is not active.");
            }

            return RiskRuleResult.Pass();
        }
    }
}
