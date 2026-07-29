using Grpc.Core;
using ReferenceDataService.Domain.Instruments;
using ReferenceDataService.Grpc.Mapping;
using Serilog.Context;
using TradingApp.Shared.Messaging.Correlation;
using PlatformAssetClass =
    TradingApp.SharedKernel.AssetClass;

using DomainFxDetails =
    ReferenceDataService.Domain.Instruments.FxInstrumentDetails;

using GrpcAssetClass =
    ReferenceDataService.Grpc.AssetClass;

using GrpcFxDetails =
    ReferenceDataService.Grpc.FxInstrumentDetails;

namespace ReferenceDataService.Grpc.Services
{
    public class ReferenceDataGrpcService : ReferenceData.ReferenceDataBase
    {
        private readonly IInstrumentRepository instrumentRepository;
        private readonly IInstrumentGrpcMapper instrumentMapper;
        private readonly ILogger<ReferenceDataGrpcService> logger;

        public ReferenceDataGrpcService(
            IInstrumentRepository instrumentRepository,
            IInstrumentGrpcMapper instrumentMapper,
            ILogger<ReferenceDataGrpcService> logger)
        {
            this.instrumentRepository = instrumentRepository;
            this.instrumentMapper = instrumentMapper;
            this.logger = logger;
        }

        public override Task<GetInstrumentResponse> GetInstrument(GetInstrumentRequest request, ServerCallContext context)
        {
            var correlationId = context.RequestHeaders.GetValue(GrpcCorrelationConstants.MetadataKey);
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = "Not_Set";
            }

            using (LogContext.PushProperty(GrpcCorrelationConstants.MetadataKey, correlationId))
            {
                logger.LogInformation(
                "Instrument lookup started. Symbol={Symbol}, CorrelationId={CorrelationId}",
                request.Symbol,
                correlationId);

                var definition = instrumentRepository.GetBySymbol(request.Symbol);

                if (definition == null)
                {
                    logger.LogWarning(
                        "Instrument was not found. Symbol={Symbol}, CorrelationId={CorrelationId}",
                        request.Symbol,
                        correlationId);

                    throw new RpcException(
                        new Status(StatusCode.NotFound, $"Instrument '{request.Symbol}' was not found."));
                }

                var response = instrumentMapper.Map(definition);

                logger.LogInformation(
                    "Instrument lookup completed. Symbol={Symbol}, AssetClass={AssetClass}, CorrelationId={CorrelationId}",
                    response.Symbol,
                    response.AssetClass,
                    correlationId);

                return Task.FromResult(response);
            }
        }

        
    }
}
