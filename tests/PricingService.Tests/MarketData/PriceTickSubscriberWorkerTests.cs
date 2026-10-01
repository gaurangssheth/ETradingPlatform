using FluentAssertions;
using NetMQ;
using NetMQ.Sockets;
using PricingService.Grpc.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;

namespace PricingService.Tests.MarketData
{
    public sealed class PriceTickSubscriberWorkerTests
    {
        [Fact]
        public async Task Run_UpdatesLatestQuoteStore_WhenPriceTickIsReceived()
        {
            using var publisher = new PublisherSocket();

            var endpoint = BindPublisher(publisher);

            var quoteCache = new MarketQuoteCache();
            var quoteStream = new MarketQuoteStream();
            var candleStore = new MarketCandleStore();
            var candleAggregator = new CandleAggregator(candleStore);
            var candleStream = new MarketCandleStream();

            var worker = new PriceTickSubscriberWorker(quoteCache, quoteStream, candleAggregator, candleStream, endpoint);

            using var cancellationTokenSource = new CancellationTokenSource();

            var workerTask = Task.Run(() =>
            {
                worker.Run(cancellationTokenSource.Token);
            });

            var expectedTick =
                new PriceTick(
                    Symbol: "AAPL",
                    Bid: 210.00m,
                    Ask: 210.50m,
                    Timestamp: DateTimeOffset.UtcNow);

            var payload = JsonSerializer.Serialize(expectedTick);

            PriceTick? storedTick = null;

            for (var attempt = 0; attempt < 10; attempt++)
            {
                publisher
                    .SendMoreFrame(expectedTick.Symbol)
                    .SendFrame(payload);

                await Task.Delay(100);

                if (quoteCache.TryGet(
                        expectedTick.Symbol,
                        out storedTick))
                {
                    break;
                }
            }

            cancellationTokenSource.Cancel();

            await workerTask.WaitAsync(TimeSpan.FromSeconds(2));

            storedTick.Should().Be(expectedTick);
        }

        [Fact]
        public async Task Run_PublishesMarketQuote_WhenPriceTickIsReceived()
        {
            using var publisher = new PublisherSocket();

            var endpoint = BindPublisher(publisher);

            var quoteCache = new MarketQuoteCache();
            var quoteStream = new MarketQuoteStream();
            var candleStore = new MarketCandleStore();
            var candleAggregator = new CandleAggregator(candleStore);
            var candleStream = new MarketCandleStream();

            var worker = new PriceTickSubscriberWorker(
                quoteCache,
                quoteStream,
                candleAggregator,
                candleStream,
                endpoint);

            using var cancellationTokenSource = new CancellationTokenSource();

            var workerTask = Task.Run(() =>
            {
                worker.Run(cancellationTokenSource.Token);
            });

            var quoteTask = ReadNextQuoteAsync(quoteStream, cancellationTokenSource.Token);

            var expectedTick = new PriceTick(
                Symbol: "AAPL",
                Bid: 210.00m,
                Ask: 210.50m,
                Timestamp: DateTimeOffset.UtcNow);

            var payload = JsonSerializer.Serialize(expectedTick);

            for (var attempt = 0; attempt < 10 && !quoteTask.IsCompleted; attempt++)
            {
                publisher
                    .SendMoreFrame(expectedTick.Symbol)
                    .SendFrame(payload);

                await Task.Delay(100);
            }

            var receivedTick = await quoteTask.WaitAsync(TimeSpan.FromSeconds(2));

            cancellationTokenSource.Cancel();

            await workerTask.WaitAsync(TimeSpan.FromSeconds(2));

            receivedTick.Should().Be(expectedTick);
        }

        [Fact]
        public async Task Run_PublishesMarketCandle_WhenPriceTickIsReceived()
        {
            using var publisher = new PublisherSocket();

            var endpoint = BindPublisher(publisher);

            var quoteCache = new MarketQuoteCache();
            var quoteStream = new MarketQuoteStream();
            var candleStore = new MarketCandleStore();
            var candleAggregator = new CandleAggregator(candleStore);
            var candleStream = new MarketCandleStream();

            var worker = new PriceTickSubscriberWorker(
                quoteCache,
                quoteStream,
                candleAggregator,
                candleStream,
                endpoint);

            using var cancellationTokenSource = new CancellationTokenSource();

            var workerTask = Task.Run(() =>
            {
                worker.Run(cancellationTokenSource.Token);
            });

            var expectedTick = new PriceTick(
                Symbol: "AAPL",
                Bid: 210.00m,
                Ask: 210.50m,
                Timestamp: new DateTimeOffset(
                    2026, 9, 26, 10, 34, 33, TimeSpan.Zero));

            var payload = JsonSerializer.Serialize(expectedTick);

            var candleTask = ReadNextCandleAsync(
                candleStream,
                cancellationTokenSource.Token);

            for (var attempt = 0; attempt < 10 && !candleTask.IsCompleted; attempt++)
            {
                publisher
                    .SendMoreFrame(expectedTick.Symbol)
                    .SendFrame(payload);

                await Task.Delay(100);
            }

            var candle = await candleTask.WaitAsync(TimeSpan.FromSeconds(2));

            candle.Symbol.Should().Be("AAPL");
            candle.StartTime.Should().Be(
                new DateTimeOffset(2026, 9, 26, 10, 34, 30, TimeSpan.Zero));

            candle.Symbol.Should().Be("AAPL");
            candle.Open.Should().Be(210.25m);
            candle.High.Should().Be(210.25m);
            candle.Low.Should().Be(210.25m);
            candle.Close.Should().Be(210.25m);

            cancellationTokenSource.Cancel();

            await workerTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        private static string BindPublisher(PublisherSocket publisher)
        {
            var port =
                publisher.BindRandomPort(
                    "tcp://127.0.0.1");

            return $"tcp://127.0.0.1:{port}";
        }

        private static async Task<PriceTick> ReadNextQuoteAsync(MarketQuoteStream stream,
            CancellationToken cancellationToken)
        {
            await foreach (var tick in stream.ReadAllAsync(cancellationToken))
            {
                return tick;
            }

            throw new InvalidOperationException(
                "Market quote stream completed without returning a tick.");
        }

        private static async Task<MarketCandle> ReadNextCandleAsync(MarketCandleStream stream,
            CancellationToken cancellationToken)
        {
            await foreach(var candle in stream.ReadAllAsync(cancellationToken))
            {
                return candle;
            }

            throw new InvalidOperationException("Market candle stream completed without returning a candle.");
        }
    }
}
