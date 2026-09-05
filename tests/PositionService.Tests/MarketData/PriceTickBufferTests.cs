using FluentAssertions;
using PositionService.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;

namespace PositionService.Tests.MarketData
{
    public class PriceTickBufferTests
    {
        [Fact]
        public async Task TakeLatest_ShouldReturnOnlyLatestTickForEachSymbol()
        {
            var buffer = new PriceTickBuffer();

            buffer.Publish(new PriceTick("EURUSD", 1.0840m, 1.0832m, DateTimeOffset.UtcNow));
            buffer.Publish(new PriceTick("EURUSD", 1.0850m, 1.0852m, DateTimeOffset.UtcNow));
            buffer.Publish(new PriceTick("EURUSD", 1.0860m, 1.0862m, DateTimeOffset.UtcNow));
            buffer.Publish(new PriceTick("AAPL", 210.00m, 210.50m, DateTimeOffset.UtcNow));

            await buffer.WaitForUpdatesAsync(CancellationToken.None);

            var ticks = buffer.TakeLatest();

            ticks.Should().HaveCount(2);

            ticks.Should().ContainSingle(
                tick =>
                    tick.Symbol == "EURUSD" &&
                    tick.Bid == 1.0860m &&
                    tick.Ask == 1.0862m);

            ticks.Should().ContainSingle(
                tick =>
                    tick.Symbol == "AAPL" &&
                    tick.Bid == 210.00m &&
                    tick.Ask == 210.50m);

        }

        [Fact]
        public async Task Publish_ShouldKeepSingleLatestTickPerSymbol_WhenPublishedConcurrently()
        {
            var buffer = new PriceTickBuffer();

            var ticks = Enumerable
                .Range(1, 100)
                .Select(index =>
                    new PriceTick(
                        "EURUSD",
                        1.0800m + index / 10000m,
                        1.0802m + index / 10000m,
                        DateTimeOffset.UtcNow.AddMilliseconds(index)))
                .ToArray();

            var publishTasks = ticks
                .Select(tick =>
                    Task.Run(() => buffer.Publish(tick)))
                .ToArray();

            await Task.WhenAll(publishTasks);

            await buffer.WaitForUpdatesAsync(CancellationToken.None);

            var bufferedTicks = buffer.TakeLatest();

            bufferedTicks.Should().ContainSingle();

            var bufferedTick = bufferedTicks.Single();

            bufferedTick.Symbol.Should().Be("EURUSD");

            ticks.Should().Contain(bufferedTick);
        }

        [Fact]
        public async Task Publish_ShouldKeepNewestTickByTimestamp_WhenPublishedConcurrently()
        {
            var buffer = new PriceTickBuffer();

            var baseTimestamp = DateTimeOffset.UtcNow;

            var ticks = Enumerable
                .Range(1, 100)
                .Select(index =>
                    new PriceTick(
                        "EURUSD",
                        1.0800m + index / 10000m,
                        1.0802m + index / 10000m,
                        baseTimestamp.AddMilliseconds(index)))
                .ToArray();

            var publishTasks = ticks.Select(tick => Task.Run(() => buffer.Publish(tick))).ToArray();

            await Task.WhenAll(publishTasks);

            await buffer.WaitForUpdatesAsync(CancellationToken.None);

            var bufferedTicks = buffer.TakeLatest();

            bufferedTicks.Should().ContainSingle();

            bufferedTicks.Single().Should().Be(ticks[^1]);
        }

        [Fact]
        public async Task Publish_ShouldKeepNewestTickByTimestamp_WhenTicksArriveOutOfOrder()
        {
            var buffer = new PriceTickBuffer();

            var baseTimestamp = DateTimeOffset.UtcNow;

            var newestTick = new PriceTick(
                "EURUSD",
                1.0860m,
                1.0862m,
                baseTimestamp.AddMilliseconds(300));

            var oldestTick = new PriceTick(
                "EURUSD",
                1.0840m,
                1.0842m,
                baseTimestamp.AddMilliseconds(100));

            var middleTick = new PriceTick(
                "EURUSD",
                1.0850m,
                1.0852m,
                baseTimestamp.AddMilliseconds(200));

            // Deliberately not timestamp order.
            buffer.Publish(newestTick);
            buffer.Publish(oldestTick);
            buffer.Publish(middleTick);

            await buffer.WaitForUpdatesAsync(CancellationToken.None);

            var ticks = buffer.TakeLatest();

            ticks.Should().ContainSingle();

            ticks.Single().Should().Be(newestTick);
        }
    }
}
