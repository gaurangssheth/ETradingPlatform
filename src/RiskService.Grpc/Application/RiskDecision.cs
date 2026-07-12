namespace RiskService.Grpc.Application;

public sealed class RiskDecision
{
    public bool Approved { get; init; }

    public string ReasonCode { get; init; } = null!;

    public string Reason { get; init; } = null!;

    public Guid RiskDecisionId { get; init; } = Guid.NewGuid();

    public static RiskDecision Approve()
    {
        return new RiskDecision
        {
            Approved = true,
            ReasonCode = RiskReasonCodes.Approved,
            Reason = "Order approved by risk checks."
        };
    }

    public static RiskDecision Reject(string reasonCode, string reason)
    {
        return new RiskDecision
        {
            Approved = false,
            ReasonCode = reasonCode,
            Reason = reason
        };
    }
}