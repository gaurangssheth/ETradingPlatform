using ReferenceDataService.Domain.Instruments;
using DomainAssetClass =
    ReferenceDataService.Domain.Instruments.AssetClass;

using DomainBondDetails =
    ReferenceDataService.Domain.Instruments.BondInstrumentDetails;

using DomainDayCountConvention =
    ReferenceDataService.Domain.Instruments.DayCountConvention;

using DomainEquityDetails =
    ReferenceDataService.Domain.Instruments.EquityInstrumentDetails;

using DomainFxDetails =
    ReferenceDataService.Domain.Instruments.FxInstrumentDetails;

using GrpcAssetClass =
    ReferenceDataService.Grpc.AssetClass;

using GrpcDayCountConvention =
    ReferenceDataService.Grpc.DayCountConvention;

using GrpcEquityDetails =
    ReferenceDataService.Grpc.EquityInstrumentDetails;

using GrpcFxDetails =
    ReferenceDataService.Grpc.FxInstrumentDetails;

using GrpcBondDetails =
    ReferenceDataService.Grpc.BondInstrumentDetails;

namespace ReferenceDataService.Grpc.Mapping
{
    public class InstrumentGrpcMapper : IInstrumentGrpcMapper
    {
        public GetInstrumentResponse Map(InstrumentDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);

            var response = new GetInstrumentResponse
            {
                InstrumentId = definition.Instrument.InstrumentId.ToString(),
                Symbol = definition.Instrument.Symbol,
                AssetClass = MapAssetClass(definition.Instrument.AssetClass),
                IsTradable = definition.Instrument.IsTradable
            };

            switch (definition.Details)
            {
                case DomainFxDetails fxDetails:
                    response.FxDetails = new GrpcFxDetails
                    {
                        BaseCurrency = fxDetails.BaseCurrency.Value,
                        QuoteCurrency = fxDetails.QuoteCurrency.Value,
                        PipSize = Convert.ToDouble(fxDetails.PipSize)
                    };
                    break;
                case DomainEquityDetails equityDetails:
                    response.EquityDetails = new GrpcEquityDetails
                    {
                        Exchange = equityDetails.Exchange,
                        TradingCurrency = equityDetails.TradingCurrency.Value
                    };
                    break;
                case DomainBondDetails bondDetails:
                    response.BondDetails = new GrpcBondDetails
                    {
                        Isin = bondDetails.Isin,
                        Issuer = bondDetails.Issuer,
                        CouponRate = Convert.ToDouble(bondDetails.CouponRate),
                        MaturityDate = bondDetails.MaturityDate.ToString("yyyy-MM-dd"),
                        ParValue = Convert.ToDouble(bondDetails.ParValue),
                        DayCountConvention = MapDayCountConvetion(bondDetails.DayCountConvention)
                    };
                    break;
                default:
                    throw new NotSupportedException(
                    $"Instrument details type '{definition.Details.GetType().Name}' is not supported.");
            }

            return response;
        }

        private static Grpc.AssetClass MapAssetClass(DomainAssetClass assetClass)
        {
            return assetClass switch
            {
                DomainAssetClass.Fx => GrpcAssetClass.Fx,
                DomainAssetClass.Equity => GrpcAssetClass.Equity,
                DomainAssetClass.FixedIncome => GrpcAssetClass.FixedIncome,
                _ => GrpcAssetClass.Unspecified
            };
        }

        private static GrpcDayCountConvention MapDayCountConvetion(DomainDayCountConvention dayCountConvention)
        {
            return dayCountConvention switch
            {
                DomainDayCountConvention.ActualActual => GrpcDayCountConvention.ActualActual,
                DomainDayCountConvention.Actual360 => GrpcDayCountConvention.Actual360,
                DomainDayCountConvention.Actual365 => GrpcDayCountConvention.Actual365,
                DomainDayCountConvention.Thirty360 => GrpcDayCountConvention.Thirty360,
                _ => GrpcDayCountConvention.Unspecified
            };
        }
    }
}
