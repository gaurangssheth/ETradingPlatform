using Google.Protobuf;
using Grpc.Core;
using PositionService.Application.Positions;
using PositionService.Application.Queries;
using PositionService.Grpc;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.Messaging.Correlation;

namespace PositionService.Services
{
    public sealed class PositionGrpcService : global::PositionService.Grpc.PositionService.PositionServiceBase
    {
        private readonly IQueryDispatcher queryDispatcher;
        private ILogger<PositionGrpcService> logger;

        public PositionGrpcService(IQueryDispatcher queryDispatcher, ILogger<PositionGrpcService> logger)
        {
            this.queryDispatcher = queryDispatcher;
            this.logger = logger;
        }

        public override async Task<GetOpenPositionsByClientIdResponse> GetOpenPositionsByClientId(GetOpenPositionsByClientIdRequest request, ServerCallContext context)
        {
            var correlationId = context.RequestHeaders.GetValue(GrpcCorrelationConstants.MetadataKey);
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = "Not_Set";
            }

            using (LogContext.PushProperty(GrpcCorrelationConstants.MetadataKey, correlationId))
            {
                logger.LogInformation(
                    "Open positions lookup started. ClientId={ClientId}, CorrelationId={CorrelationId}",
                    request.ClientId,
                    correlationId);

                var positions = await queryDispatcher.SendAsync<
                GetOpenPositionsByClientIdQuery,
                IReadOnlyList<PositionSummaryReadModel>>(
                    new GetOpenPositionsByClientIdQuery(request.ClientId), context.CancellationToken);

                var response = new GetOpenPositionsByClientIdResponse();

                response.Positions.AddRange(
                    positions.Select(position => new PositionSummary
                    {
                        InstrumentId = position.InstrumentId.ToString(),
                        Symbol = position.Symbol,
                        AssetClass = position.AssetClass.ToString(),
                        NetQuantity = position.NetQuantity.ToString(CultureInfo.InvariantCulture),
                        AveragePrice = position.AveragePrice.ToString(CultureInfo.InvariantCulture),
                        PnlCurrency = position.PnlCurrency.Value,
                        RealisedPnl = position.RealisedPnl.ToString(CultureInfo.InvariantCulture),
                        UnrealisedPnl = position.UnrealisedPnl.ToString(CultureInfo.InvariantCulture),
                        TotalPnl = position.TotalPnl.ToString(CultureInfo.InvariantCulture)
                    }));

                logger.LogInformation(
                    "Open positions lookup completed. ClientId={ClientId}, PositionCount={PositionCount}, CorrelationId={CorrelationId}",
                    request.ClientId,
                    response.Positions.Count,
                    correlationId);


                return response;
            }
                
        }        
    }
}
