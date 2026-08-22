using NetMQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;

namespace MarketDataSimulator
{
    internal sealed class PriceTickPublisherWorker
    {
        private readonly ChannelReader<PriceTick> reader;
        private readonly string endPoint;

        public PriceTickPublisherWorker(ChannelReader<PriceTick> reader, string endPoint)
        {
            this.reader = reader;
            this.endPoint = endPoint;
        }

        public void Run(CancellationToken cancellationToken)
        {
            using var runtime =
                new NetMQRuntime();

            var publishTask = PublishAsync(cancellationToken);

            runtime.Run(cancellationToken, publishTask);

            publishTask.GetAwaiter().GetResult();
        }

        private async Task PublishAsync(CancellationToken cancellationToken)
        {
            using var publisher = new ZeroMqPricePublisher(this.endPoint);

            Console.WriteLine("Publisher worker waiting for PriceTicks...");

            try
            {
                await foreach (var tick in this.reader.ReadAllAsync(cancellationToken))
                {
                    Console.WriteLine($"Publisher worker received {tick.Symbol}");

                    publisher.Publish(tick);
                }
            }
            finally
            {
                Console.WriteLine(
                    "Publisher worker exiting.");
            }
        }
    }
}
