using NetMQ;
using NetMQ.Sockets;
using System.Text.Json;
using TradingApp.MarketData.Contracts;

namespace PricingService.Grpc.MarketData
{
    public sealed class ZeroMqPriceSubscriber : IDisposable
    {
        private readonly SubscriberSocket subscriberSocket;

        public ZeroMqPriceSubscriber(string endpoint)
        {
            this.subscriberSocket = new SubscriberSocket();
            this.subscriberSocket.Connect(endpoint);
            this.subscriberSocket.Subscribe(string.Empty); // Subscribe to all messages

            Console.WriteLine($"Market data subscriber connected to {endpoint}");
        }

        public bool TryReceive(TimeSpan timeSpan, out PriceTick? tick)
        {
            var frames = new List<string>();

            if (!this.subscriberSocket.TryReceiveMultipartStrings(timeSpan, ref frames))
            {
                tick = null;
                return false;
            }

            if (frames.Count < 2)
            {
                throw new InvalidOperationException(
                    $"Expected 2 market-data frames but received {frames.Count}.");
            }

            var topic = frames[0];
            var payload = frames[1];

            tick = JsonSerializer.Deserialize<PriceTick>(payload) ??
                throw new InvalidOperationException(
                    $"Failed to deserialize market-data payload: {payload}");

            if (!string.Equals(
                topic,
                tick.Symbol,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Market-data topic '{topic}' does not match payload symbol '{tick.Symbol}'.");
            }

            return true;


        }

        public void Dispose()
        {
            this.subscriberSocket.Dispose();
        }
    }
}
