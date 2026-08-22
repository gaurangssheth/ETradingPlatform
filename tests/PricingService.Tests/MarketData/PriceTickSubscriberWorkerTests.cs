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

            var store = new MarketQuoteCache();

            var worker = new PriceTickSubscriberWorker(store, endpoint);

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

                if (store.TryGet(
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

        private static string BindPublisher(PublisherSocket publisher)
        {
            var port =
                publisher.BindRandomPort(
                    "tcp://127.0.0.1");

            return $"tcp://127.0.0.1:{port}";
        }
    }
}
