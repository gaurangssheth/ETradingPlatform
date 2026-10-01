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
using TradingGateway.Api.ClientModels;
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

        [Fact]
        public async Task StreamMarketQuotesAsync_ShouldMapStreamingResponse()
        {
            var timestamp = DateTimeOffset.UtcNow;

            var responseStream = new TestAsyncStreamReader<MarketQuote>(
                new[]
                {
                    new MarketQuote
                    {
                        Symbol = "EURUSD",
                        Bid = "1.0850",
                        Ask = "1.0852",
                        Timestamp = timestamp.ToString("O")
                    }
                }
            );

            var call = CreateAsyncServerStreamingCall<MarketQuote>(responseStream);

            var pricingClient = new Mock<Pricing.PricingClient>();

            pricingClient.Setup(x => x.StreamMarketQuotes(
                It.IsAny<GetMarketQuotesRequest>(),
                It.IsAny<Metadata>(),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(call);

            var client = new GrpcPricingServiceClient(pricingClient.Object, 
                NullLogger<GrpcPricingServiceClient>.Instance);

            var results = new List<MarketQuoteResponse>();

            await foreach (var quote in client.StreamMarketQuotesAsync("correlation-001"))
            {
                results.Add(quote);
            }

            results.Should().HaveCount(1);

            results[0].Symbol.Should().Be("EURUSD");
            results[0].Bid.Should().Be(1.0850m);
            results[0].Ask.Should().Be(1.0852m);
            results[0].Timestamp.Should().Be(timestamp);
        }

        [Fact]
        public async Task StreamMarketQuotesAsync_WhenCorrelationIdProvided_ShouldSendCorrelationIdInGrpcMetadata()
        {
            var timestamp = DateTimeOffset.UtcNow;

            var responseStream = new TestAsyncStreamReader<MarketQuote>(
                new[]
                {
                    new MarketQuote
                    {
                        Symbol = "EURUSD",
                        Bid = "1.0850",
                        Ask = "1.0852",
                        Timestamp = timestamp.ToString("O")
                    }
                }
            );

            Metadata? capturedHeaders = null;

            var call = CreateAsyncServerStreamingCall<MarketQuote>(responseStream);

            var pricingClient = new Mock<Pricing.PricingClient>();

            pricingClient.Setup(x => x.StreamMarketQuotes(
                It.IsAny<GetMarketQuotesRequest>(),
                It.IsAny<Metadata>(),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<
                GetMarketQuotesRequest,
                Metadata,
                DateTime?,
                CancellationToken>(
                (_, headers, _, _) =>
                {
                    capturedHeaders = headers;
                })
            .Returns(call);

            var client = new GrpcPricingServiceClient(pricingClient.Object,
                NullLogger<GrpcPricingServiceClient>.Instance);

            await foreach (var _ in client.StreamMarketQuotesAsync("correlation-001"))
            {
                break;
            }

            capturedHeaders.Should().NotBeNull();

            capturedHeaders!
                .GetValue(GrpcCorrelationConstants.MetadataKey)
                .Should().Be("correlation-001");

        }

        [Fact]
        public async Task GetMarketCandlesAsync_ShouldCallGrpcClientAndReturnMarketCandleResponses()
        {
            var grpcResponse = new GetMarketCandlesResponse();

            grpcResponse.Candles.Add(new PricingService.Grpc.MarketCandle
            {
                Symbol = "EURUSD",
                StartTime = "2026-09-26T08:30:00.0000000+00:00",
                Open = "1.0850",
                High = "1.0860",
                Low = "1.0840",
                Close = "1.0855"
            });

            var grpcClientMock = new Mock<Pricing.PricingClient>();

            grpcClientMock
                .Setup(client => client.GetMarketCandlesAsync(
                    It.IsAny<GetMarketCandlesRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(grpcResponse));

            var client = new GrpcPricingServiceClient(
                grpcClientMock.Object,
                NullLogger<GrpcPricingServiceClient>.Instance);

            var result = await client.GetMarketCandlesAsync("EURUSD");

            result.Should().ContainSingle();

            var candle = result.Single();

            candle.Symbol.Should().Be("EURUSD");
            candle.Open.Should().Be(1.0850m);
            candle.High.Should().Be(1.0860m);
            candle.Low.Should().Be(1.0840m);
            candle.Close.Should().Be(1.0855m);

            candle.StartTime.Should().Be(
                new DateTimeOffset(
                    2026, 9, 26, 8, 30, 0,
                    TimeSpan.Zero));
        }

        [Fact]
        public async Task GetMarketCandlesAsync_WhenCorrelationIdProvided_ShouldSendCorrelationIdInGrpcMetadata()
        {
            var grpcResponse =
                new GetMarketCandlesResponse();

            Metadata? capturedHeaders = null;

            var grpcClientMock =
                new Mock<Pricing.PricingClient>();

            grpcClientMock
                .Setup(client => client.GetMarketCandlesAsync(
                    It.IsAny<GetMarketCandlesRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<
                    GetMarketCandlesRequest,
                    Metadata,
                    DateTime?,
                    CancellationToken>(
                    (_, headers, _, _) =>
                    {
                        capturedHeaders = headers;
                    })
                .Returns(CreateAsyncUnaryCall(grpcResponse));

            var client = new GrpcPricingServiceClient(
                grpcClientMock.Object,
                NullLogger<GrpcPricingServiceClient>.Instance);

            await client.GetMarketCandlesAsync(
                "EURUSD",
                "candles-correlation-test-001",
                CancellationToken.None);

            capturedHeaders.Should().NotBeNull();

            capturedHeaders!
                .GetValue(GrpcCorrelationConstants.MetadataKey)
                .Should()
                .Be("candles-correlation-test-001");
        }

        [Fact]
        public async Task StreamMarketCandlesAsync_ShouldMapStreamingResponse()
        {
            var startTime = new DateTimeOffset(2026, 9, 26, 18, 30, 0, TimeSpan.Zero);

            var responseStream = new TestAsyncStreamReader<MarketCandle>(
                new[]
                {
                    new MarketCandle
                    {
                        Symbol = "EURUSD",
                        StartTime = startTime.ToString("O"),
                        Open = "1.0850",
                        High = "1.0860",
                        Low = "1.0840",
                        Close = "1.0855"
                    }
                });

            using var call = CreateAsyncServerStreamingCall<MarketCandle>(responseStream);

            var pricingClient = new Mock<Pricing.PricingClient>();

            pricingClient
                .Setup(x => x.StreamMarketCandles(
                    It.IsAny<StreamMarketCandlesRequest>(),
                    It.IsAny<Metadata>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(call);

            var client = new GrpcPricingServiceClient(
                pricingClient.Object,
                NullLogger<GrpcPricingServiceClient>.Instance);

            var results = new List<MarketCandleResponse>();

            await foreach (var candle in client.StreamMarketCandlesAsync("candle-correlation-001"))
            {
                results.Add(candle);
            }

            results.Should().ContainSingle();

            results[0].Symbol.Should().Be("EURUSD");
            results[0].StartTime.Should().Be(startTime);
            results[0].Open.Should().Be(1.0850m);
            results[0].High.Should().Be(1.0860m);
            results[0].Low.Should().Be(1.0840m);
            results[0].Close.Should().Be(1.0855m);
        }

        [Fact]
        public async Task StreamMarketCandlesAsync_WhenCorrelationIdProvided_ShouldSendCorrelationIdInGrpcMetadata()
        {
            Metadata? capturedHeaders = null;

            var responseStream = new TestAsyncStreamReader<MarketCandle>(
                new[]
                {
                    new MarketCandle
                    {
                        Symbol = "EURUSD",
                        StartTime = new DateTimeOffset(2026, 9, 26, 18, 30, 0, TimeSpan.Zero).ToString("O"),
                        Open = "1.0850",
                        High = "1.0860",
                        Low = "1.0840",
                        Close = "1.0855"
                    }
                });

            using var call =
                CreateAsyncServerStreamingCall<MarketCandle>(
                    responseStream);

            var pricingClient =
                new Mock<Pricing.PricingClient>();

            pricingClient
                .Setup(x => x.StreamMarketCandles(
                    It.IsAny<StreamMarketCandlesRequest>(),
                    It.IsAny<Metadata>(),
                    null,
                    It.IsAny<CancellationToken>()))
                .Callback<
                    StreamMarketCandlesRequest,
                    Metadata,
                    DateTime?,
                    CancellationToken>(
                    (_, headers, _, _) =>
                    {
                        capturedHeaders = headers;
                    })
                .Returns(call);

            var client = new GrpcPricingServiceClient(
                pricingClient.Object,
                NullLogger<GrpcPricingServiceClient>.Instance);

            await foreach (
                var _ in client.StreamMarketCandlesAsync("candle-correlation-001"))
            {
                break;
            }

            capturedHeaders.Should().NotBeNull();

            capturedHeaders!
                .GetValue(GrpcCorrelationConstants.MetadataKey)
                .Should()
                .Be("candle-correlation-001");
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

        private static AsyncServerStreamingCall<T> CreateAsyncServerStreamingCall<T>(TestAsyncStreamReader<T> responseStream)
        {
            return new AsyncServerStreamingCall<T>(
                responseStream,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        private sealed class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
        {
            private IEnumerator<T> enumerator;

            public TestAsyncStreamReader(IEnumerable<T> items)
            {
                this.enumerator = items.GetEnumerator();    
            }

            public T Current => this.enumerator.Current;

            public Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                return Task.FromResult<bool>(this.enumerator.MoveNext());
            }
        }
    }
}
