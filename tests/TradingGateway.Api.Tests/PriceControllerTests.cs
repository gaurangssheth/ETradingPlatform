using FluentAssertions;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PricingService.Grpc;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;
using TradingGateway.Api.Controllers;
using TradingGateway.Api.Prices;

namespace TradingGateway.Api.Tests;

public class PricesControllerTests
{
    [Fact]
    public async Task GetPrice_WhenSymbolExists_ShouldReturnOkWithPriceQuote()
    {
        var grpcResponse = new GetPriceResponse
        {
            Symbol = "EURUSD",
            Bid = 1.0849,
            Ask = 1.0851,
            Mid = 1.0850
        };

        var pricingClient = new Mock<Pricing.PricingClient>();

        pricingClient
            .Setup(x => x.GetPriceAsync(
                It.Is<GetPriceRequest>(r => r.Symbol == "EURUSD"),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(grpcResponse));

        var controller = new PricesController(
            pricingClient.Object,
            NullLogger<PricesController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetPrice("EURUSD", CancellationToken.None);

        var okResult = result.Result
            .Should()
            .BeOfType<OkObjectResult>()
            .Subject;

        var response = okResult.Value
            .Should()
            .BeOfType<PriceQuoteResponse>()
            .Subject;

        response.Symbol.Should().Be("EURUSD");
        response.Bid.Should().Be(1.0849m);
        response.Ask.Should().Be(1.0851m);
        response.Mid.Should().Be(1.0850m);

        pricingClient.Verify(x => x.GetPriceAsync(
                It.Is<GetPriceRequest>(r => r.Symbol == "EURUSD"),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPrice_WhenSymbolExists_ShouldSendCorrelationIdMetadataAndReturnOk()
    {
        var grpcResponse = new GetPriceResponse
        {
            Symbol = "EURUSD",
            Bid = 1.0849,
            Ask = 1.0851,
            Mid = 1.0850
        };

        Metadata? capturedHeaders = null;

        var pricingClient = new Mock<Pricing.PricingClient>();

        pricingClient.Setup(x => x.GetPriceAsync(
            It.Is<GetPriceRequest>(r => r.Symbol == "EURUSD"),
            It.IsAny<Metadata>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()))
            .Callback<GetPriceRequest, Metadata, DateTime?, CancellationToken>((_, headers, _, _) =>
            {
                capturedHeaders = headers;
            }).Returns(CreateAsyncUnaryCall(grpcResponse));

        var controller = new PricesController(
            pricingClient.Object,
            NullLogger<PricesController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        controller.HttpContext.Request.Headers[CorrelationConstants.HeaderName] = "gateway-price-correlation-001";

        var result = await controller.GetPrice("EURUSD", CancellationToken.None);

        var okResult = result.Result
            .Should()
            .BeOfType<OkObjectResult>()
            .Subject;

        var response = okResult.Value
            .Should()
            .BeOfType<PriceQuoteResponse>()
            .Subject;

        response.Symbol.Should().Be("EURUSD");
        response.Bid.Should().Be(1.0849m);
        response.Ask.Should().Be(1.0851m);
        response.Mid.Should().Be(1.0850m);

        capturedHeaders.Should().NotBeNull();
        capturedHeaders.Should().ContainSingle(m => m.Key == GrpcCorrelationConstants.MetadataKey && m.Value == "gateway-price-correlation-001");
        capturedHeaders!
            .GetValue(GrpcCorrelationConstants.MetadataKey)
            .Should()
            .Be("gateway-price-correlation-001");
    }

    [Fact]
    public async Task GetPrice_WhenPricingServiceReturnsNotFound_ShouldReturnNotFound()
    {
        var pricingClient = new Mock<Pricing.PricingClient>();

        pricingClient
            .Setup(x => x.GetPriceAsync(
                It.Is<GetPriceRequest>(r => r.Symbol == "ABCXYZ"),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateFaultedAsyncUnaryCall<GetPriceResponse>(
                new RpcException(new Status(
                    StatusCode.NotFound,
                    "No price configured for symbol ABCXYZ."))));

        var controller = new PricesController(
            pricingClient.Object,
            NullLogger<PricesController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetPrice("ABCXYZ", CancellationToken.None);

        var notFoundResult = result.Result
            .Should()
            .BeOfType<NotFoundObjectResult>()
            .Subject;

        notFoundResult.StatusCode.Should().Be(404);
        notFoundResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPrice_WhenPricingServiceReturnsInvalidArgument_ShouldReturnBadRequest()
    {
        var pricingClient = new Mock<Pricing.PricingClient>();

        pricingClient
            .Setup(x => x.GetPriceAsync(
                It.Is<GetPriceRequest>(r => r.Symbol == ""),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateFaultedAsyncUnaryCall<GetPriceResponse>(
                new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "Symbol is required."))));

        var controller = new PricesController(
            pricingClient.Object,
            NullLogger<PricesController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetPrice("", CancellationToken.None);

        var badRequestResult = result.Result
            .Should()
            .BeOfType<BadRequestObjectResult>()
            .Subject;

        badRequestResult.StatusCode.Should().Be(400);
        badRequestResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPrice_WhenPricingServiceIsUnavailable_ShouldReturnServiceUnavailable()
    {
        var pricingClient = new Mock<Pricing.PricingClient>();

        pricingClient
            .Setup(x => x.GetPriceAsync(
                It.Is<GetPriceRequest>(r => r.Symbol == "EURUSD"),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateFaultedAsyncUnaryCall<GetPriceResponse>(
                new RpcException(new Status(
                    StatusCode.Unavailable,
                    "Pricing service unavailable."))));

        var controller = new PricesController(
            pricingClient.Object,
            NullLogger<PricesController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetPrice("EURUSD", CancellationToken.None);

        var objectResult = result.Result
            .Should()
            .BeOfType<ObjectResult>()
            .Subject;

        objectResult.StatusCode.Should().Be(503);
        objectResult.Value.Should().NotBeNull();
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

    private static AsyncUnaryCall<T> CreateFaultedAsyncUnaryCall<T>(RpcException exception)
    {
        return new AsyncUnaryCall<T>(
            Task.FromException<T>(exception),
            Task.FromResult(new Metadata()),
            () => new Status(exception.StatusCode, exception.Status.Detail),
            () => new Metadata(),
            () => { });
    }
}