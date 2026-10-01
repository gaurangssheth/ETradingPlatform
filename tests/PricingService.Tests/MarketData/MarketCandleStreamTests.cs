using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using PricingService.Grpc.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PricingService.Tests.MarketData
{
    public class MarketCandleStreamTests
    {
        [Fact]
        public async Task Publish_ShouldMakeCandleAvailableToReader()
        {
            var stream = new MarketCandleStream();

            var candle = new MarketCandle(
                Symbol: "EURUSD",
                StartTime: new DateTimeOffset(
                    2026, 9, 26, 10, 34, 30, TimeSpan.Zero),
                Open: 1.0850m,
                High: 1.0854m,
                Low: 1.0848m,
                Close: 1.0852m);

            stream.Publish(candle);

            using var cancellationTokenSource = new CancellationTokenSource();

            await using var enumerator = stream.ReadAllAsync(cancellationTokenSource.Token)
                    .GetAsyncEnumerator();

            var hasItem = await enumerator
                .MoveNextAsync()
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(1));

            hasItem.Should().BeTrue();
            enumerator.Current.Should().Be(candle);

            cancellationTokenSource.Cancel();
        }
    }
}
