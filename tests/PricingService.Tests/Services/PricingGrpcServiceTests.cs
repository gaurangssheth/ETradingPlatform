using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PricingService.Grpc;
using PricingService.Grpc.MarketData;
using PricingService.Grpc.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;


namespace PricingService.Tests.Services
{
    public class PricingGrpcServiceTests
    {
        [Fact]
        public async Task GetPrice_Should_Return_EurUsd_Price()
        {
            var marketQuoteCache = new MarketQuoteCache();

            marketQuoteCache.Update(new PriceTick
            (
                "EURUSD",
                1.0849m,
                1.0851m,
                DateTimeOffset.UtcNow
            ));


            var service = CreateService(marketQuoteCache);

            var response = await service.GetPrice(new Grpc.GetPriceRequest
            {
                Symbol = "EURUSD"
            }, TestServerCallContext.Create());

            response.Symbol.Should().Be("EURUSD");
            response.Mid.Should().BeApproximately(1.0850, 0.0000001);
            response.Bid.Should().BeApproximately(1.0849, 0.0000001);
            response.Ask.Should().BeApproximately(1.0851, 0.0000001);
            response.Should().BeOfType<Grpc.GetPriceResponse>();
        }

        [Fact]
        public async Task GetPrice_Should_Normalise_Symbol()
        {
            var marketQuoteCache = new MarketQuoteCache();

            marketQuoteCache.Update(
                new PriceTick(
                "EURUSD",
                1.0849m,
                1.0851m,
                DateTimeOffset.UtcNow));

            var service = CreateService(marketQuoteCache);

            var response = await service.GetPrice(
                new GetPriceRequest { Symbol = " eurusd " },
                TestServerCallContext.Create());

            response.Symbol.Should().Be("EURUSD");
            response.Mid.Should().Be(1.0850);
        }

        [Fact]
        public async Task GetPrice_Should_Throw_When_MarketData_Is_Not_Available()
        {
            var marketQuoteCache = new MarketQuoteCache();
            var service = CreateService(marketQuoteCache);

            Func<Task> action = async () => await service.GetPrice(
                new GetPriceRequest { Symbol = "ABCXYZ" },
                TestServerCallContext.Create());

            var exception = await action.Should().ThrowAsync<RpcException>();

            exception.Which.StatusCode.Should().Be(StatusCode.Unavailable);
        }

        [Fact]
        public async Task GetPrice_Should_Throw_When_Symbol_Is_Empty()
        {
            var marketQuoteCache = new MarketQuoteCache();
            var service = CreateService(marketQuoteCache);

            var action = async () => await service.GetPrice(
                    new GetPriceRequest { Symbol = "" },
                    TestServerCallContext.Create());

            var exception = await action.Should().ThrowAsync<RpcException>();

            exception.Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
        }

        [Fact]
        public async Task GetPrice_WhenCorrelationIdHeaderExists_ShouldStillReturnPrice()
        {
            var marketQuoteCache = new MarketQuoteCache();
            var service = CreateService(marketQuoteCache);

            marketQuoteCache.Update(
                new PriceTick(
                "EURUSD",
                1.0849m,
                1.0851m,
                DateTimeOffset.UtcNow));

            var headers = new Metadata
            {
                { GrpcCorrelationConstants.MetadataKey, "pricing-service-test-001" }
            };

            var response = await service.GetPrice(
                new GetPriceRequest { Symbol = "EURUSD" },
                TestServerCallContext.Create(headers));

            response.Symbol.Should().Be("EURUSD");
            response.Bid.Should().BeApproximately(1.0849, 0.0000001);
            response.Ask.Should().BeApproximately(1.0851, 0.0000001);
        }

        [Theory]
        [InlineData("AAPL", 210.00, 210.50, 210.25)]
        [InlineData("GB00TEST1234", 98.40, 98.50, 98.45)]
        public async Task GetPrice_WhenMultiAssetSymbolConfigured_ShouldReturnExpectedQuote(
                        string symbol,
                        double expectedBid,
                        double expectedAsk,
                        double expectedMid)
        {
            var marketQuoteCache = new MarketQuoteCache();
            var service = CreateService(marketQuoteCache);

            marketQuoteCache.Update(
                new PriceTick(
                symbol,
                (decimal)expectedBid,
                (decimal)expectedAsk,
                DateTimeOffset.UtcNow));

            var response = await service.GetPrice(
                new GetPriceRequest
                {
                    Symbol = symbol
                },
                TestServerCallContext.Create());

            response.Symbol.Should().Be(symbol);

            response.Bid.Should()
                .BeApproximately(expectedBid, 0.0000001);

            response.Ask.Should()
                .BeApproximately(expectedAsk, 0.0000001);

            response.Mid.Should()
                .BeApproximately(expectedMid, 0.0000001);
        }

        [Fact]
        public async Task GetMarketQuotes_ShouldReturnQuotesFromCache()
        {
            var marketQuoteCache = new MarketQuoteCache();
            var service = CreateService(marketQuoteCache);

            var timestamp = new DateTimeOffset(2026, 9, 16, 18, 5, 47, TimeSpan.Zero);

            marketQuoteCache.Update(new PriceTick(
                Symbol: "EURUSD",
                Bid: 1.0850m,
                Ask: 1.0852m,
                Timestamp: timestamp
            ));

            var response = await service.GetMarketQuotes(
                new GetMarketQuotesRequest(),
                TestServerCallContext.Create());

            response.Quotes.Should().ContainSingle();

            var quote = response.Quotes.Single();

            quote.Symbol.Should().Be("EURUSD");
            quote.Bid.Should().Be("1.0850");
            quote.Ask.Should().Be("1.0852");
            quote.Timestamp.Should().Be(timestamp.ToString("O"));
        }

        [Fact]
        public async Task GetMarketQuotes_WhenCorrelationIdHeaderExistsShouldReturnQuotesFromCache()
        {
            var marketQuoteCache = new MarketQuoteCache();
            var service = CreateService(marketQuoteCache);

            var timestamp = new DateTimeOffset(2026, 9, 16, 18, 5, 47, TimeSpan.Zero);

            marketQuoteCache.Update(new PriceTick(
                Symbol: "EURUSD",
                Bid: 1.0850m,
                Ask: 1.0852m,
                Timestamp: timestamp
            ));

            var headers = new Metadata
            {
                {
                    GrpcCorrelationConstants.MetadataKey,
                    "pricing-test-001"
                }
            };

            var response = await service.GetMarketQuotes(
                new GetMarketQuotesRequest(),
                TestServerCallContext.Create(headers));

            response.Quotes.Should().ContainSingle();

            var quote = response.Quotes.Single();

            quote.Symbol.Should().Be("EURUSD");
            quote.Bid.Should().Be("1.0850");
            quote.Ask.Should().Be("1.0852");
            quote.Timestamp.Should().Be(timestamp.ToString("O"));
        }

        [Fact]
        public async Task GetMarketQuotes_ReturnsQuotesOrderedBySymbol()
        {
            var timestamp = DateTimeOffset.UtcNow;

            var marketQuoteCache = new MarketQuoteCache();
            var service = CreateService(marketQuoteCache);

            marketQuoteCache.Update(
                new PriceTick(
                    "EURUSD",
                    1.0850m,
                    1.0852m,
                    timestamp));

            marketQuoteCache.Update(
                new PriceTick(
                    "AAPL",
                    208.25m,
                    208.75m,
                    timestamp));

            var response = await service.GetMarketQuotes(
                new GetMarketQuotesRequest(),
                TestServerCallContext.Create());

            response.Quotes
                .Select(x => x.Symbol)
                .Should()
                .ContainInOrder(
                    "AAPL",
                    "EURUSD");
        }

        [Fact]
        public async Task StreamMarketQuotes_ShouldStreamMarketQuotes()
        {
            var marketQuoteStream = new MarketQuoteStream();
            var service = CreateService(marketQuoteStream: marketQuoteStream);

            using var cancellationTokenSource = new CancellationTokenSource();

            var headers = new Metadata
            {
                {
                    GrpcCorrelationConstants.MetadataKey,
                    "stream-test-001"
                }
            };

            var context = TestServerCallContext.Create(headers, cancellationTokenSource.Token);

           var writtenQuoteTask = new TaskCompletionSource<MarketQuote>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var responseStream = new Mock<IServerStreamWriter<MarketQuote>>();
            responseStream.Setup(x => x.WriteAsync(
                It.IsAny<MarketQuote>(),
                It.IsAny<CancellationToken>())).Callback<MarketQuote, CancellationToken>((quote, _) =>
                {
                    writtenQuoteTask.SetResult(quote);
                }).Returns(Task.CompletedTask);

            var streamTask = service.StreamMarketQuotes(new GetMarketQuotesRequest(), 
                responseStream.Object, context);

            marketQuoteStream.Publish(new PriceTick(
                Symbol: "EURUSD",
                Bid: 1.0850m,
                Ask: 1.0852m,
                Timestamp: DateTimeOffset.UtcNow));

            var writtenQuote = await writtenQuoteTask.Task;

            writtenQuote.Should().NotBeNull();
            writtenQuote!.Symbol.Should().Be("EURUSD");
            writtenQuote.Bid.Should().Be("1.0850");
            writtenQuote.Ask.Should().Be("1.0852");

            cancellationTokenSource.Cancel();

            await streamTask;
        }

        [Fact]
        public async Task GetMarketCandles_ShouldReturnCandlesFromStore()
        {
            var candleStore = new MarketCandleStore();

            var startTime = new DateTimeOffset(
                2026, 9, 26, 8, 30, 0, TimeSpan.Zero);

            candleStore.Complete(
                new PricingService.Grpc.MarketData.MarketCandle(
                    Symbol: "EURUSD",
                    StartTime: startTime,
                    Open: 1.0850m,
                    High: 1.0860m,
                    Low: 1.0840m,
                    Close: 1.0855m));

            var service = CreateService(
                marketCandleStore: candleStore);

            var headers = new Metadata
            {
                {
                    GrpcCorrelationConstants.MetadataKey,
                    "candles-service-test-001"
                }
            };

            var response = await service.GetMarketCandles(
                new GetMarketCandlesRequest
                {
                    Symbol = "EURUSD"
                },
                TestServerCallContext.Create(headers));

            response.Candles.Should().ContainSingle();

            var candle = response.Candles.Single();

            candle.Symbol.Should().Be("EURUSD");
            candle.StartTime.Should().Be(startTime.ToString("O"));
            candle.Open.Should().Be("1.0850");
            candle.High.Should().Be("1.0860");
            candle.Low.Should().Be("1.0840");
            candle.Close.Should().Be("1.0855");
        }

        [Fact]
        public async Task StreamMarketCandles_ShouldStreamMarketCandles()
        {
            var marketCandleStream = new MarketCandleStream();

            var service = CreateService(
                marketCandleStream: marketCandleStream);

            using var cancellationTokenSource = new CancellationTokenSource();

            var headers = new Metadata
            {
                {
                    GrpcCorrelationConstants.MetadataKey,
                    "candle-stream-test-001"
                }
            };

            var context = TestServerCallContext.Create(
                headers,
                cancellationTokenSource.Token);

            var writtenCandleSource =
                new TaskCompletionSource<PricingService.Grpc.MarketCandle>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            var responseStream =
                new Mock<IServerStreamWriter<PricingService.Grpc.MarketCandle>>();

            responseStream
                .Setup(x => x.WriteAsync(
                    It.IsAny<PricingService.Grpc.MarketCandle>(),
                    It.IsAny<CancellationToken>()))
                .Callback<PricingService.Grpc.MarketCandle, CancellationToken>(
                    (candle, _) =>
                    {
                        writtenCandleSource.TrySetResult(candle);
                    })
                .Returns(Task.CompletedTask);

            var streamTask = service.StreamMarketCandles(
                new StreamMarketCandlesRequest(),
                responseStream.Object,
                context);

            marketCandleStream.Publish(
                new PricingService.Grpc.MarketData.MarketCandle(
                    Symbol: "EURUSD",
                    StartTime: new DateTimeOffset(
                        2026, 9, 26, 18, 30, 0, TimeSpan.Zero),
                    Open: 1.0850m,
                    High: 1.0860m,
                    Low: 1.0840m,
                    Close: 1.0855m));

            var writtenCandle = await writtenCandleSource.Task
                .WaitAsync(TimeSpan.FromSeconds(2));

            writtenCandle.Symbol.Should().Be("EURUSD");
            writtenCandle.Open.Should().Be("1.0850");
            writtenCandle.High.Should().Be("1.0860");
            writtenCandle.Low.Should().Be("1.0840");
            writtenCandle.Close.Should().Be("1.0855");

            cancellationTokenSource.Cancel();

            await streamTask;
        }

        private static PricingGrpcService CreateService(
            MarketQuoteCache? marketQuoteCache = null,
            MarketQuoteStream? marketQuoteStream = null,
            MarketCandleStore? marketCandleStore = null,
            MarketCandleStream? marketCandleStream = null)
        {
            return new PricingGrpcService(
                marketQuoteCache ?? new MarketQuoteCache(),
                marketQuoteStream ?? new MarketQuoteStream(),
                marketCandleStore ?? new MarketCandleStore(),
                marketCandleStream ?? new MarketCandleStream(),
                NullLogger<PricingGrpcService>.Instance);
        }
    }
}
