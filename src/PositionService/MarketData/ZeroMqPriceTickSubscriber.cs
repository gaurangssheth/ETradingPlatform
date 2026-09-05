using NetMQ;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;

namespace PositionService.MarketData
{
    public class ZeroMqPriceTickSubscriber
    {
        private readonly PriceTickBuffer priceTickBuffer;
        private readonly ILogger<ZeroMqPriceTickSubscriber> logger;

        public ZeroMqPriceTickSubscriber(PriceTickBuffer priceTickBuffer, ILogger<ZeroMqPriceTickSubscriber> logger)
        {
            this.priceTickBuffer = priceTickBuffer;
            this.logger = logger;
        }

        public void Run(string endpoint, CancellationToken cancellationToken)
        {
            var subscriber = new NetMQ.Sockets.SubscriberSocket();
            subscriber.Connect(endpoint);
            subscriber.Subscribe(string.Empty); // Subscribe to all messages

            logger.LogInformation("ZeroMQ market data subscriber connected to {Endpoint}.",
                endpoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!subscriber.TryReceiveFrameString(TimeSpan.FromMilliseconds(250), out var topic))
                    {
                        continue; // No message received, continue the loop
                    }

                    if (!subscriber.TryReceiveFrameString(out var payload))
                    {
                        logger.LogWarning("Received market data topic {Topic} without payload.", topic);
                        continue;
                    }

                    var tick = JsonSerializer.Deserialize<PriceTick>(payload);

                    if (tick is null)
                    {
                        logger.LogWarning(
                            "Unable to deserialize PriceTick for topic {Topic}.",
                            topic);

                        continue;
                    }

                    if (!string.Equals(topic, tick.Symbol, StringComparison.Ordinal))
                    {
                        logger.LogWarning(
                            "Market data topic {Topic} does not match PriceTick symbol {Symbol}.",
                            topic,
                            tick.Symbol);

                        continue;
                    }

                    priceTickBuffer.Publish(tick);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while receiving price tick.");
                }
            }
        }
    }
}
