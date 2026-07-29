using FluentAssertions;
using Grpc.Core;
using Moq;
using ReferenceDataService.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.ReferenceData;
using TradingApp.Shared.Messaging.Correlation;
using TradingApp.SharedKernel;
using GrpcAssetClass = ReferenceDataService.Grpc.AssetClass;
using ReferenceDataGrpcClient =
    ReferenceDataService.Grpc.ReferenceData.ReferenceDataClient;

namespace TradeCaptureService.Tests.ReferenceData
{
    public class GrpcReferenceDataClientTests
    {
        [Fact]
        public async Task GetInstrumentAsync_Should_Map_Common_Instrument_Fields()
        {
            var instrumentId = Guid.NewGuid();

            var grpcResponse = new GetInstrumentResponse
            {
                InstrumentId = instrumentId.ToString(),
                Symbol = "AAPL",
                AssetClass = GrpcAssetClass.Equity,
                IsTradable = true,
                EquityDetails = new EquityInstrumentDetails
                {
                    Exchange = "NASDAQ",
                    TradingCurrency = "USD"
                }
            };

            var grpcClient = new Mock<ReferenceDataGrpcClient>();
            grpcClient.Setup(x => x.GetInstrumentAsync(
                It.Is<GetInstrumentRequest>(request => request.Symbol == "AAPL"),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(grpcResponse));

            var client = new GrpcReferenceDataClient(grpcClient.Object);

            var result = await client.GetInstrumentAsync("AAPL");

            result.Instrument.InstrumentId.Should().Be(instrumentId);
            result.Instrument.Symbol.Should().Be("AAPL");
            result.Instrument.AssetClass.Should().Be(TradingApp.SharedKernel.AssetClass.Equity);
            result.Instrument.IsTradable.Should().BeTrue();
            result.Details.NotionalCurrency.Should().Be(new CurrencyCode("USD"));
        }

        [Fact]
        public async Task GetInstrumentAsync_Should_Add_CorrelationId_To_Metadata()
        {
            Metadata? capturedHeaders = null;

            var grpcResponse = new GetInstrumentResponse
            {
                InstrumentId = Guid.NewGuid().ToString(),
                Symbol = "EURUSD",
                AssetClass = GrpcAssetClass.Fx,
                IsTradable = true,
                FxDetails = new FxInstrumentDetails
                {
                    BaseCurrency = "EUR",
                    QuoteCurrency = "USD",
                    PipSize = 0.0001d
                }
            };

            var grpcClient = new Mock<ReferenceDataGrpcClient>();

            grpcClient
                .Setup(x => x.GetInstrumentAsync(
                    It.IsAny<GetInstrumentRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<GetInstrumentRequest, Metadata, DateTime?, CancellationToken>(
                    (_, headers, _, _) => capturedHeaders = headers)
                .Returns(CreateAsyncUnaryCall(grpcResponse));

            var client = new GrpcReferenceDataClient(grpcClient.Object);

            await client.GetInstrumentAsync(
                "EURUSD",
                "correlation-123");

            capturedHeaders.Should().NotBeNull();

            capturedHeaders!
                .GetValue(GrpcCorrelationConstants.MetadataKey)
                .Should()
                .Be("correlation-123");
        }

        [Fact]
        public async Task GetInstrumentAsync_Should_Throw_When_InstrumentId_Is_Invalid()
        {
            var grpcResponse = new GetInstrumentResponse
            {
                InstrumentId = "not-a-guid",
                Symbol = "AAPL",
                AssetClass = GrpcAssetClass.Equity,
                IsTradable = true
            };

            var grpcClient = new Mock<ReferenceDataGrpcClient>();

            grpcClient
                .Setup(x => x.GetInstrumentAsync(
                    It.IsAny<GetInstrumentRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(grpcResponse));

            var client = new GrpcReferenceDataClient(grpcClient.Object);

            var act = () => client.GetInstrumentAsync("AAPL");

            await act.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage(
                    "*invalid InstrumentId*not-a-guid*AAPL*");
        }

        private static AsyncUnaryCall<T> CreateAsyncUnaryCall<T>(T response)
        {
            return new AsyncUnaryCall<T>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }
    }
}
