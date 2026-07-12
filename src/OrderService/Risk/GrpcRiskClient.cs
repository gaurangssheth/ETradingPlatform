using Grpc.Core;
using RiskService.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;
using TradingApp.Shared.Messaging.Correlation;

namespace OrderService.Risk
{
    public sealed class GrpcRiskClient : IRiskClient
    {
        private readonly RiskService.Grpc.Risk.RiskClient riskClient;
        private readonly ILogger<GrpcRiskClient> logger;

        public GrpcRiskClient(RiskService.Grpc.Risk.RiskClient riskClient)
        {
            this.riskClient = riskClient;
        }

        public async Task<RiskCheckResult> CheckOrderRiskAsync(SubmitOrder order, CancellationToken cancellationToken = default)
        {
            var headers = new Metadata();

            if (!string.IsNullOrWhiteSpace(order.CorrelationId))
            {
                headers.Add(GrpcCorrelationConstants.MetadataKey, order.CorrelationId);
            }

            var response = await riskClient.CheckOrderRiskAsync(
                new CheckOrderRiskRequest
                {
                    OrderId = order.OrderId.ToString(),
                    ClientId = order.ClientId,
                    Symbol = order.Symbol,
                    Side = order.Side.ToString(),
                    Quantity = Convert.ToDouble(order.Quantity),
                    OrderType = order.OrderType.ToString()
                }, headers: headers, cancellationToken: cancellationToken);

            return new RiskCheckResult
            {
                Approved = response.Approved,
                Reason = response.Reason,
                ReasonCode = response.ReasonCode,
                RiskDecisionId = Guid.Parse(response.RiskDecisionId)
            };
        }
    }
}
