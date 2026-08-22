using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;

namespace MarketDataSimulator
{
    internal sealed class ZeroMqPricePublisher : IDisposable
    {
        private readonly PublisherSocket publisherSocket;
        public ZeroMqPricePublisher(string endpoint)
        {
            publisherSocket = new PublisherSocket();
            publisherSocket.Bind(endpoint);

            Console.WriteLine($"Publisher bound to {endpoint}");
        }

        public void Publish(PriceTick tick)
        {
            var payload = JsonSerializer.Serialize(tick);

            publisherSocket
                .SendMoreFrame(tick.Symbol)
                .SendFrame(payload);

            Console.WriteLine(
                $"{tick.Timestamp:HH:mm:ss.fff} " +
                $"{tick.Symbol} Bid={tick.Bid} Ask={tick.Ask}");
        }

        public void Dispose()
        {
            publisherSocket?.Dispose();
        }
    }
}
