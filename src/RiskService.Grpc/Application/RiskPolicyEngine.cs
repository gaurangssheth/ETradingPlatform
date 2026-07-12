using RiskService.Grpc.Application.Rules;

namespace RiskService.Grpc.Application
{
    public class RiskPolicyEngine
    {
        private IEnumerable<IRiskRule> riskRules;
        private ILogger<RiskPolicyEngine> logger;

        public RiskPolicyEngine(
            IEnumerable<IRiskRule> riskRules,
            ILogger<RiskPolicyEngine> logger)
        {
            this.riskRules = riskRules;
            this.logger = logger;
        }

        public RiskDecision Check(RiskCheckRequestModel request)
        {
            foreach (var rule in riskRules)
            {
                var result = rule.Check(request);
                if (!result.Passed)
                {
                    logger.LogWarning(
                    "Risk rule failed. Rule={Rule}, OrderId={OrderId}, ClientId={ClientId}, Symbol={Symbol}, ReasonCode={ReasonCode}, Reason={Reason}",
                    rule.GetType().Name,
                    request.OrderId,
                    request.ClientId,
                    request.Symbol,
                    result.ReasonCode,
                    result.Reason);

                    return RiskDecision.Reject(result.ReasonCode, result.Reason);
                }
            }
            logger.LogInformation(
            "Risk checks approved. OrderId={OrderId}, ClientId={ClientId}, Symbol={Symbol}, Quantity={Quantity}",
            request.OrderId,
            request.ClientId,
            request.Symbol,
            request.Quantity);

            return RiskDecision.Approve();
        }
    }
}
