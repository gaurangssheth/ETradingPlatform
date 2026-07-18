using ReferenceDataService.Domain.Instruments;

namespace ReferenceDataService.Grpc.Mapping
{
    public interface IInstrumentGrpcMapper
    {
        GetInstrumentResponse Map(InstrumentDefinition definition);
    }
}
