using Grpc.Core;
using ReferenceDataService.Grpc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Domain;
using TradingApp.Shared.Messaging.Correlation;

using GrpcAssetClass = ReferenceDataService.Grpc.AssetClass;
using GrpcDayCountConvention = ReferenceDataService.Grpc.DayCountConvention;
using PlatformAssetClass = TradingApp.SharedKernel.AssetClass;
using PlatformDayCountConvention =
    TradingApp.SharedKernel.DayCountConvention;

namespace TradeCaptureService.ReferenceData
{
    public sealed class GrpcReferenceDataClient : IReferenceDataClient
    {
        private readonly ReferenceDataService.Grpc.ReferenceData.ReferenceDataClient client;

        public GrpcReferenceDataClient(ReferenceDataService.Grpc.ReferenceData.ReferenceDataClient client)
        {
            this.client = client;
        }
        public async Task<InstrumentReferenceDefinition> GetInstrumentAsync(string symbol, string? correlationId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException(
                    "Instrument symbol cannot be empty.",
                    nameof(symbol));
            }

            var headers = new Metadata();

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                headers.Add(GrpcCorrelationConstants.MetadataKey, correlationId);
            }

            var response = await client.GetInstrumentAsync(
                new ReferenceDataService.Grpc.GetInstrumentRequest
                {
                    Symbol = symbol
                },
                headers,
                cancellationToken: cancellationToken
            );

            if (!Guid.TryParse(response.InstrumentId, out var instrumentId))
            {
                throw new InvalidOperationException(
                    $"ReferenceDataService returned invalid InstrumentId" +
                    $"'{response.InstrumentId}' for symbol '{response.Symbol}'"
                );
            }

            var instrument = new InstrumentReferenceData
            {
                InstrumentId = instrumentId,
                Symbol = response.Symbol,
                AssetClass = MapAssetClass(response.AssetClass),
                IsTradable = response.IsTradable
            };

            var details = MapDetails(
                response,
                instrumentId);

            return new InstrumentReferenceDefinition(
                instrument,
                details);
        }

        private PlatformAssetClass MapAssetClass(GrpcAssetClass assetClass)
        {
            return assetClass switch
            {
                GrpcAssetClass.Fx => PlatformAssetClass.Fx,
                GrpcAssetClass.Equity => PlatformAssetClass.Equity,
                GrpcAssetClass.FixedIncome => PlatformAssetClass.FixedIncome,
                _ => throw new NotSupportedException(
                    $"Asset class '{assetClass}' is not supported.")
            };
        }

        private static IInstrumentReferenceDetails MapDetails(GetInstrumentResponse response, Guid instrumentId)
        {
            return response.DetailsCase switch
            {
                GetInstrumentResponse.DetailsOneofCase.FxDetails =>
                    new FxInstrumentReferenceDetails(
                        instrumentId,
                        response.FxDetails.BaseCurrency,
                        response.FxDetails.QuoteCurrency,
                        Convert.ToDecimal(response.FxDetails.PipSize)
                    ),
                
                GetInstrumentResponse.DetailsOneofCase.EquityDetails =>
                    new EquityInstrumentReferenceDetails(
                        instrumentId,
                        response.EquityDetails.Exchange,
                        response.EquityDetails.TradingCurrency),
                
                GetInstrumentResponse.DetailsOneofCase.BondDetails =>
                    MapBondDetais(response, instrumentId),
            
                _ => throw new InvalidOperationException(
                    $"ReferenceDataService returned no instrument details " +
                    $"for symbol '{response.Symbol}'.")
            };
        }

        private static BondInstrumentReferenceDetails MapBondDetais(GetInstrumentResponse response, Guid instrumentId)
        {
            if (!DateOnly.TryParseExact(response.BondDetails.MaturityDate, 
                "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var maturityDate))
            {
                throw new InvalidOperationException(
                    "ReferenceDataService returned invalid maturity Date" +
                    $"'{response.BondDetails.MaturityDate}' " +
                    $"for symbol '{response.Symbol}'");
            }

            return new BondInstrumentReferenceDetails(
                    instrumentId,
                    response.BondDetails.Isin,
                    response.BondDetails.Issuer,
                    response.BondDetails.DenominationCurrency,
                    Convert.ToDecimal(
                        response.BondDetails.CouponRate),
                    maturityDate,
                    Convert.ToDecimal(
                        response.BondDetails.ParValue),
                    MapDayCountConvention(
                        response.BondDetails.DayCountConvention));
        }

        private static PlatformDayCountConvention MapDayCountConvention(GrpcDayCountConvention dayCountConvention)
        {
            return dayCountConvention switch
            {
                GrpcDayCountConvention.ActualActual => PlatformDayCountConvention.ActualActual,
                GrpcDayCountConvention.Actual360 => PlatformDayCountConvention.Actual360,
                GrpcDayCountConvention.Actual365 => PlatformDayCountConvention.Actual365,
                GrpcDayCountConvention.Thirty360 => PlatformDayCountConvention.Thirty360,
                _ => throw new NotSupportedException(
                    $"Day-count convention '{dayCountConvention}' " +
                    "is not supported.")
            };
        }
    }
}
