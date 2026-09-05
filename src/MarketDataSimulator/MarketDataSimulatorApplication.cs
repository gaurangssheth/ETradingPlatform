using NetMQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;

namespace MarketDataSimulator
{
    public sealed class MarketDataSimulatorApplication
    {
        private const string PublisherEndpoint = "tcp://*:5555";

        public async Task RunAsync()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            
            ConsoleCancelEventHandler cancelHandler =
            (_, eventArgs) =>
            {
                eventArgs.Cancel = true;

                cancellationTokenSource.Cancel();

                Console.WriteLine("Cancellation requested. Stopping the application...");
            };

            Console.CancelKeyPress += cancelHandler;

            var producerTasks = Array.Empty<Task>();

            try
            {
                var priceTickChannel = CreatePriceTickChannel();

                var simulators = CreateInstrumentSimulators(priceTickChannel.Writer);

                producerTasks = simulators.Select(simulator =>
                    simulator.RunAsync(cancellationTokenSource.Token)).ToArray();

                var publisherWorker = new PriceTickPublisherWorker(priceTickChannel.Reader, PublisherEndpoint);

                try
                {
                    publisherWorker.Run(cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                    when (cancellationTokenSource.IsCancellationRequested)
                {
                    Console.WriteLine("MarketDataSimulator stopped.");
                }

                try
                {
                    await Task.WhenAll(producerTasks);
                }
                catch (OperationCanceledException)
                    when (cancellationTokenSource.IsCancellationRequested)
                {
                    Console.WriteLine("All price simulators stopped.");
                }
            }
            
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    $"MarketDataSimulator failed: {exception}");

                throw;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        private static Channel<PriceTick> CreatePriceTickChannel()
        {
            return Channel.CreateBounded<PriceTick>(new BoundedChannelOptions(100)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        private static InstrumentPriceSimulator[] CreateInstrumentSimulators(ChannelWriter<PriceTick> writer)
        {
            return
            [
                new InstrumentPriceSimulator(
                    writer,
                    symbol: "EURUSD",
                    initialBid: 1.0849m,
                    spread: 0.0002m,
                    priceStep: 0.0001m,
                    minimumDelayMilliseconds: 100,
                    maximumDelayMilliseconds: 400),
                
                new InstrumentPriceSimulator(
                    writer,
                    symbol: "AAPL",
                    initialBid: 210.00m,
                    spread: 0.50m,
                    priceStep: 0.25m,
                    minimumDelayMilliseconds: 250,
                    maximumDelayMilliseconds: 800),

                new InstrumentPriceSimulator(
                    writer,
                    symbol: "GB00TEST1234",
                    initialBid: 98.40m,
                    spread: 0.10m,
                    priceStep: 0.05m,
                    minimumDelayMilliseconds: 500,
                    maximumDelayMilliseconds: 1500)
            ];
        }
    }
}
