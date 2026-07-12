namespace RiskService.Grpc.Application.Rules
{
    public interface IRiskRule
    {
        RiskRuleResult Check(RiskCheckRequestModel request);
    }
}
