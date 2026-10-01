using FluentAssertions;
using PricingService.Grpc;
using PricingService.Grpc.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;

namespace PricingService.Tests.MarketData
{
    public class MarketQuoteStreamTests
    {
        [Fact]
        public async Task Publish_ShouldMakeQuoteAvailableToReader()
        {
            var stream = new MarketQuoteStream();

            var tick = new PriceTick(
                Symbol: "EURUSD",
                Bid: 1.0850m,
                Ask: 1.0852m,
                Timestamp: new DateTimeOffset(
                    2026, 9, 26, 10, 34, 30, TimeSpan.Zero));

            stream.Publish(tick);

            using var cancellationTokenSource = new CancellationTokenSource();

            await using var enumerator =
                stream.ReadAllAsync(cancellationTokenSource.Token)
                    .GetAsyncEnumerator();

            var hasItem = await enumerator
                .MoveNextAsync()
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(1));

            hasItem.Should().BeTrue();
            enumerator.Current.Should().Be(tick);

            cancellationTokenSource.Cancel();
        }
    }
}
