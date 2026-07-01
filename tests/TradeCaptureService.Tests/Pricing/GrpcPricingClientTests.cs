using FluentAssertions;
using Google.Protobuf;
using Grpc.Core;
using Moq;
using PricingService.Grpc;
using TradeCaptureService.Pricing;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;

namespace TradeCaptureService.Tests.Pricing;

public class GrpcPricingClientTests
{
    [Fact]
    public async Task GetPriceAsync_Should_Call_Grpc_Client_And_Return_PriceQuote()
    {
        var grpcResponse = new GetPriceResponse
        {
            Symbol = "EURUSD",
            Bid = 1.0849,
            Ask = 1.0851,
            Mid = 1.0850
        };

        var asyncUnaryCall = CreateAsyncUnaryCall(grpcResponse);

        var grpcClientMock = new Mock<PricingService.Grpc.Pricing.PricingClient>();

        grpcClientMock
            .Setup(x => x.GetPriceAsync(
                It.Is<GetPriceRequest>(r => r.Symbol == "EURUSD"),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(asyncUnaryCall);

        var client = new GrpcPricingClient(grpcClientMock.Object);

        var result = await client.GetPriceAsync("EURUSD");

        result.Symbol.Should().Be("EURUSD");
        result.Bid.Should().Be(1.0849m);
        result.Ask.Should().Be(1.0851m);
        result.Mid.Should().Be(1.0850m);

        grpcClientMock.Verify(x => x.GetPriceAsync(
                It.Is<GetPriceRequest>(r => r.Symbol == "EURUSD"),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPriceAsync_WhenCorrelationIdProvided_ShouldSendCorrelationIdInGrpcMetadata()
    {
        var grpcResponse = new GetPriceResponse
        {
            Symbol = "EURUSD",
            Bid = 1.0849,
            Ask = 1.0851,
            Mid = 1.0850
        };

        Metadata? capturedHeaders = null;

        var pricingClient = new Mock<PricingService.Grpc.Pricing.PricingClient>();

        pricingClient
            .Setup(x => x.GetPriceAsync(
                It.Is<GetPriceRequest>(r => r.Symbol == "EURUSD"),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<GetPriceRequest, Metadata, DateTime?, CancellationToken>(
                (_, headers, _, _) =>
                {
                    capturedHeaders = headers;
                })
            .Returns(CreateAsyncUnaryCall(grpcResponse));

        var client = new GrpcPricingClient(pricingClient.Object);

        var result = await client.GetPriceAsync(
            "EURUSD",
            "grpc-correlation-test-001",
            CancellationToken.None);

        result.Symbol.Should().Be("EURUSD");

        capturedHeaders.Should().NotBeNull();
        capturedHeaders!.GetValue(GrpcCorrelationConstants.MetadataKey)
            .Should()
            .Be("grpc-correlation-test-001");
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
