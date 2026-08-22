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
    public sealed class ZeroMqPriceSubscriberTests
    {
        [Fact]
        public void TryReceive_ReceivesAndDeserializesPriceTick()
        {
            using var publisher = new PublisherSocket();
            
            var endpoint = BindPublisher(publisher);

            using var subscriber = new ZeroMqPriceSubscriber(endpoint);

            var expectedTick = new PriceTick
            (
                Symbol: "EURUSD",
                Bid: 1.0849m,
                Ask: 1.0851m,
                Timestamp: DateTimeOffset.UtcNow
            );

            var payload = JsonSerializer.Serialize(expectedTick);

            PriceTick? receivedTick = null;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                publisher.SendMoreFrame(expectedTick.Symbol)
                    .SendFrame(payload);

                var received = subscriber.TryReceive(TimeSpan.FromMicroseconds(100), out receivedTick);

                if (received)
                {
                    break;
                }
            }

            receivedTick.Should().Be(expectedTick);
        }

        [Fact]
        public void TryReceive_Throws_WhenTopicDoesNotMatchPayloadSymbol()
        {
            using var publisher = new PublisherSocket();

            var endpoint = BindPublisher(publisher);

            using var subscriber =
                new ZeroMqPriceSubscriber(endpoint);

            var tick = new PriceTick(
                Symbol: "AAPL",
                Bid: 210.00m,
                Ask: 210.50m,
                Timestamp: DateTimeOffset.UtcNow);

            var payload = JsonSerializer.Serialize(tick);

            Action receive = () =>
            {
                for (var attempt = 0; attempt < 10; attempt++)
                {
                    publisher
                        .SendMoreFrame("EURUSD")
                        .SendFrame(payload);

                    subscriber.TryReceive(
                            TimeSpan.FromMilliseconds(100),
                            out _);
                }
            };

            receive.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*topic 'EURUSD'*payload symbol 'AAPL'*");
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
