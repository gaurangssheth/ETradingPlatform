using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TradingGateway.Api.BackgroundServices;
using TradingGateway.Api.ClientModels;
using TradingGateway.Api.Clients;
using TradingGateway.Api.Hubs;

namespace TradingGateway.Api.Tests.BackgroundServices
{
    public class MarketDataStreamingWorkerTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldBroadcastMarketQuoteUpdated()
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            var quote = CreateQuote();

            var quoteBroadcast = new TaskCompletionSource<(string Method, object[] Arguments)>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var streamCancelled = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var pricingServiceClient = CreatePricingServiceClient(
                quote: quote,
                candle: null,
                streamCancelled: streamCancelled, 
                cancellationToken: cancellationTokenSource.Token);

            var hubContext = CreateHubContext("MarketQuoteUpdated", quoteBroadcast, out var clientProxy);
            var serviceScopeFactory = CreateServiceScopeFactory(pricingServiceClient);
            var marketDataStreamingWorker = CreateWorker(serviceScopeFactory, hubContext);

            await marketDataStreamingWorker.StartAsync(CancellationToken.None);

            var broadcast = await quoteBroadcast.Task;

            broadcast.Method.Should().Be("MarketQuoteUpdated");
            broadcast.Arguments.Should().ContainSingle();
            broadcast.Arguments[0].Should().BeSameAs(quote);

            cancellationTokenSource.Cancel();

            await streamCancelled.Task;

            cancellationTokenSource.IsCancellationRequested.Should().BeTrue();

            await marketDataStreamingWorker.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldBroadcastMarketCandleUpdated()
        {
            using var cancellationTokenSource = new CancellationTokenSource();

            var candle = new MarketCandleResponse(
                Symbol: "EURUSD",
                StartTime: new DateTimeOffset(2026, 9, 26, 20, 45, 0, TimeSpan.Zero),
                Open: 1.0850m,
                High: 1.0860m,
                Low: 1.0840m,
                Close: 1.0855m);

            var candleBroadcast =
                new TaskCompletionSource<(string Method, object[] Arguments)>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            var streamCancelled =
                new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            var pricingServiceClient = CreatePricingServiceClient(
                quote: null,
                candle: candle,
                streamCancelled: streamCancelled,
                cancellationToken: cancellationTokenSource.Token);

            var hubContext = CreateHubContext("MarketCandleUpdated", candleBroadcast, out var clientProxy);
            var serviceScopeFactory = CreateServiceScopeFactory(pricingServiceClient);
            var marketDataStreamingWorker = CreateWorker(serviceScopeFactory, hubContext);

            await marketDataStreamingWorker.StartAsync(CancellationToken.None);

            var broadcast = await candleBroadcast.Task;

            broadcast.Method.Should().Be("MarketCandleUpdated");

            broadcast.Arguments.Should().ContainSingle();

            broadcast.Arguments[0].Should().BeSameAs(candle);

            cancellationTokenSource.Cancel();

            await streamCancelled.Task;

            await marketDataStreamingWorker.StopAsync(CancellationToken.None);

        }

        private static MarketQuoteResponse CreateQuote()
        {
            return new MarketQuoteResponse
            {
                Symbol = "EURUSD",
                Bid = 1.0850m,
                Ask = 1.0852m,
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        private static Mock<IPricingServiceClient> CreatePricingServiceClient(
            MarketQuoteResponse? quote,
            MarketCandleResponse? candle,
            TaskCompletionSource streamCancelled,
            CancellationToken cancellationToken)
        {
            var pricingServiceClient =
                new Mock<IPricingServiceClient>();

            pricingServiceClient.Setup(x => x.StreamMarketQuotesAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns((string _, CancellationToken _) => 
                    GetQuotesAsync(quote, streamCancelled, cancellationToken));

            pricingServiceClient.Setup(x => x.StreamMarketCandlesAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns((string _, CancellationToken _) =>
                    GetCandlesAsync(candle, streamCancelled, cancellationToken));

            return pricingServiceClient;
        }

        private static async IAsyncEnumerable<MarketQuoteResponse> GetQuotesAsync(
            MarketQuoteResponse? quote,
            TaskCompletionSource streamCancelled,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            if (quote is not null)
            {
                yield return quote;
            }

            try
            {
                await Task.Delay(Timeout.Infinite,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                streamCancelled.TrySetResult();

                yield break;
            }
        }

        private static async IAsyncEnumerable<MarketCandleResponse> GetCandlesAsync(
            MarketCandleResponse? candle,
            TaskCompletionSource streamCancelled,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            if (candle is not null)
            {
                yield return candle;
            }

            try
            {
                await Task.Delay(Timeout.Infinite,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                streamCancelled.TrySetResult();

                yield break;
            }
        }

        private static Mock<IHubContext<MarketDataHub>> CreateHubContext(
            string methodName,
            TaskCompletionSource<(string Method, object[] Arguments)> quoteBroadcast,
            out Mock<IClientProxy> clientProxy)
        {
            clientProxy = new Mock<IClientProxy>();

            clientProxy
                .Setup(x => x.SendCoreAsync(
                    methodName,
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, object[], CancellationToken>((method, arguments, _) =>
                {
                    quoteBroadcast.TrySetResult((method, arguments));
                })
                .Returns(Task.CompletedTask);

            var hubClients = new Mock<IHubClients>();

            hubClients
                .SetupGet(x => x.All)
                .Returns(clientProxy.Object);

            var hubContext = new Mock<IHubContext<MarketDataHub>>();

            hubContext
                .SetupGet(x => x.Clients)
                .Returns(hubClients.Object);

            return hubContext;
        }

        private static Mock<IServiceScopeFactory> CreateServiceScopeFactory(
            Mock<IPricingServiceClient> pricingServiceClient)
        {
            var serviceProvider = new Mock<IServiceProvider>();

            serviceProvider
                .Setup(x => x.GetService(
                    typeof(IPricingServiceClient)))
                .Returns(
                    pricingServiceClient.Object);

            var serviceScope = new Mock<IServiceScope>();

            serviceScope
                .SetupGet(x => x.ServiceProvider)
                .Returns(serviceProvider.Object);

            var serviceScopeFactory =
                new Mock<IServiceScopeFactory>();

            serviceScopeFactory
                .Setup(x => x.CreateScope())
                .Returns(serviceScope.Object);

            return serviceScopeFactory;
        }

        private static MarketDataStreamingWorker CreateWorker(
            Mock<IServiceScopeFactory> serviceScopeFactory,
            Mock<IHubContext<MarketDataHub>> hubContext)
        {
            

            return new MarketDataStreamingWorker(
                serviceScopeFactory.Object,
                hubContext.Object,
                NullLogger<MarketDataStreamingWorker>.Instance);
        }
    }
}
