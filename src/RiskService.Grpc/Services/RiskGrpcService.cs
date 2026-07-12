using Grpc.Core;
using RiskService.Grpc.Application;
using Serilog.Context;
using TradingApp.Contracts.Shared;
using TradingApp.Shared.Messaging.Correlation;

namespace RiskService.Grpc.Services
{
    public class RiskGrpcService : Risk.RiskBase
    {
        private readonly ILogger<RiskGrpcService> logger;
        private readonly RiskPolicyEngine riskPolicyEngine;

        public RiskGrpcService(RiskPolicyEngine riskPolicyEngine, ILogger<RiskGrpcService> logger)
        {
            this.logger = logger;
            this.riskPolicyEngine = riskPolicyEngine;
        }

        public override Task<CheckOrderRiskResponse> CheckOrderRisk(CheckOrderRiskRequest request, ServerCallContext context)
        {
            var correlationId = context.RequestHeaders.GetValue(GrpcCorrelationConstants.MetadataKey);

            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = "NOT_SET";
            }

            using (LogContext.PushProperty(GrpcCorrelationConstants.MetadataKey, correlationId))
            {
                var riskRequest = MapRequest(request);
                var decision = riskPolicyEngine.Check(riskRequest);

                logger.LogInformation("Risk descision completed. OrderId={OrderId},ClientId={ClientId}, Symbol={Symbol}, Approved={Approved}, ReasonCode={ReasonCode}, RiskDecisionId={RiskDecisionId}, CorrelationId={CorrelationId}",
                    riskRequest.OrderId, 
                    riskRequest.ClientId, 
                    riskRequest.Symbol,
                    decision.Approved,
                    decision.ReasonCode,
                    decision.RiskDecisionId, 
                    correlationId);

                return Task.FromResult(new CheckOrderRiskResponse
                {
                    Approved = decision.Approved,
                    Reason = decision.Reason,
                    ReasonCode = decision.ReasonCode,
                    RiskDecisionId = decision.RiskDecisionId.ToString()
                });
            }
        }

        private RiskCheckRequestModel MapRequest(CheckOrderRiskRequest request)
        {
            if (!Guid.TryParse(request.OrderId, out var orderId))
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "OrderId must be a valid GUID."
                ));
            }

            var symbol = request.Symbol?.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "Symbol is required."));
            }

            if (!Enum.TryParse<OrderSide>(
            request.Side?.Trim(),
            ignoreCase: true,
            out var side))
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    $"Side must be a valid value: {string.Join(", ", Enum.GetNames<OrderSide>())}."));
            }

            if (!Enum.TryParse<OrderType>(
                    request.OrderType?.Trim(),
                    ignoreCase: true,
                    out var orderType))
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    $"OrderType must be a valid value: {string.Join(", ", Enum.GetNames<OrderType>())}."));
            }

            return new RiskCheckRequestModel
            {
                OrderId = orderId,
                ClientId = request.ClientId,
                Symbol = symbol,
                Quantity = Convert.ToDecimal(request.Quantity),
                Side = side,
                OrderType = orderType
            };
        }
    }
}
