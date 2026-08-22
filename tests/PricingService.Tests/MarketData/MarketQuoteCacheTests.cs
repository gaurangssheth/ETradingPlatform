using FluentAssertions;
using PricingService.Grpc.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;

namespace PricingService.Tests.MarketData
{
    public class MarketQuoteCacheTests
    {
        [Fact]
        public void Update_StorePriceTick()
        {
            var store = new MarketQuoteCache();

            var tick = new PriceTick
            (
                Symbol: "EURUSD",
                Bid: 1.0849m,
                Ask: 1.0851m,
                Timestamp: DateTimeOffset.UtcNow
            );

            store.Update(tick);

            var found = store.TryGet("EURUSD", out var storedTick);

            found.Should().BeTrue();
            storedTick.Should().Be(tick);
        }

        [Fact]
        public void Update_ReplacesExistingPriceTickForSameSymbol()
        {
            var store = new MarketQuoteCache();

            var firstTick = new PriceTick(
                Symbol: "AAPL",
                Bid: 210.00m,
                Ask: 210.50m,
                Timestamp: DateTimeOffset.UtcNow);

            var secondTick = new PriceTick(
                Symbol: "AAPL",
                Bid: 210.25m,
                Ask: 210.75m,
                Timestamp: DateTimeOffset.UtcNow.AddMilliseconds(100));

            store.Update(firstTick);
            store.Update(secondTick);

            var found = store.TryGet("AAPL", out var storedTick);

            found.Should().BeTrue();
            storedTick.Should().Be(secondTick);
        }

        [Fact]
        public void TryGet_ReturnsFalse_WhenSymbolDoesNotExist()
        {
            var store = new MarketQuoteCache();

            var found = store.TryGet(
                "UNKNOWN",
                out var tick);

            found.Should().BeFalse();
            tick.Should().BeNull();
        }
    }
}
