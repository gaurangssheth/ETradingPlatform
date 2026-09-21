using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PositionService.Grpc;
using PricingService.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.Messaging.Correlation;
using TradingGateway.Api.Clients;

namespace TradingGateway.Api.Tests.Clients
{
    public class GrpcPricingServiceClientTests
    {
        [Fact]
        public async Task GetMarketQuotesAsync_ShouldCallGrpcClientAndReturnMarketQuotesResponse()
        {
            var grpcResponse = new GetMarketQuotesResponse();

            grpcResponse.Quotes.Add(new MarketQuote
            {
                Symbol = "EURUSD",
                Bid = "1.0850",
                Ask = "1.0852",
                Timestamp = "2026-09-16T18:05:47.0800010+00:00"
            });

            var asyncUnaryCall = CreateAsyncUnaryCall(grpcResponse);

            var grpcClientMock = new Mock<PricingService.Grpc.Pricing.PricingClient>();

            grpcClientMock.Setup(client => client.GetMarketQuotesAsync(
                It.IsAny<GetMarketQuotesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
                .Returns(asyncUnaryCall);

            var client = new GrpcPricingServiceClient(grpcClientMock.Object, NullLogger< GrpcPricingServiceClient>.Instance);

            var result = await client.GetMarketQuotesAsync();

            result.Should().ContainSingle();

            result[0].Symbol.Should().Be("EURUSD");
            result[0].Bid.Should().Be(1.0850m);
            result[0].Ask.Should().Be(1.0852m);
            result[0].Spread.Should().Be(0.0002m);

        }

        [Fact]
        public async Task GetMarketQuotesAsync_WhenCorrelationIdProvided_ShouldSendCorrelationIdInGrpcMetadata()
        {
            var grpcResponse = new GetMarketQuotesResponse();

            Metadata? capturedHeaders = null;

            var grpcClientMock = new Mock<Pricing.PricingClient>();

            grpcClientMock.Setup(client => client.GetMarketQuotesAsync(
                It.IsAny<GetMarketQuotesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
                .Callback<GetMarketQuotesRequest, Metadata, DateTime?, CancellationToken>(
                    (_, headers, _, _) => capturedHeaders = headers)
                .Returns(CreateAsyncUnaryCall(grpcResponse));

            var client = new GrpcPricingServiceClient(grpcClientMock.Object, NullLogger<GrpcPricingServiceClient>.Instance);

            await client.GetMarketQuotesAsync(
                "quotes-correlation-test-001",
                CancellationToken.None);

            capturedHeaders.Should().NotBeNull();

            capturedHeaders!.GetValue(GrpcCorrelationConstants.MetadataKey)
                .Should().Be("quotes-correlation-test-001");

        }

        private static AsyncUnaryCall<T>
        CreateAsyncUnaryCall<T>(
            T response)
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
