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
    public sealed class CandleAggregatorTests
    {
        [Fact]
        public void Add_WhenTickMovesToNextTimeWindow_CompletesPreviousCandle()
        {
            var candleStore = new MarketCandleStore();
            var cancelAgreegator = new CandleAggregator(candleStore);

            var firstTick = new PriceTick
            (
                Symbol : "EURUSD",
                Bid : 1.0848m,
                Ask : 1.0850m,
                Timestamp : new DateTimeOffset(
                    2026, 9, 25,
                    10, 53, 51,
                    TimeSpan.Zero)
            );

            var secondTick = new PriceTick
            (
                Symbol : "EURUSD",
                Bid : 1.0850m,
                Ask : 1.0852m,
                Timestamp : new DateTimeOffset(
                    2026, 9, 25,
                    10, 53, 56,
                    TimeSpan.Zero)
            );

            cancelAgreegator.Add(firstTick);
            cancelAgreegator.Add(secondTick);

            var candles = candleStore.GetCandles("EURUSD");

            candles.Should().HaveCount(2);

            candles[0].StartTime.Should().Be(
                new DateTimeOffset(
                    2026, 9, 25,
                    10, 53, 50,
                    TimeSpan.Zero));

            candles[1].StartTime.Should().Be(
                new DateTimeOffset(
                    2026, 9, 25,
                    10, 53, 55,
                    TimeSpan.Zero));
        }

        [Fact]
        public void Add_WhenTicksAreInSameTimeWindow_UpdatesOpenHighLowAndClose()
        {
            var candleStore = new MarketCandleStore();
            var candleAggregator = new CandleAggregator(candleStore);

            var firstTick = new PriceTick
            (
                Symbol : "EURUSD",
                Bid : 1.0848m,
                Ask : 1.0850m,
                Timestamp : new DateTimeOffset(
                    2026, 9, 25,
                    10, 53, 51,
                    TimeSpan.Zero)
            );

            var secondTick = new PriceTick
            (
                Symbol : "EURUSD",
                Bid : 1.0852m,
                Ask : 1.0854m,
                Timestamp : new DateTimeOffset(
                    2026, 9, 25,
                    10, 53, 52,
                    TimeSpan.Zero)
            );

            var thirdTick = new PriceTick
            (
                Symbol : "EURUSD",
                Bid : 1.0844m,
                Ask : 1.0846m,
                Timestamp : new DateTimeOffset(
                    2026, 9, 25,
                    10, 53, 54,
                    TimeSpan.Zero)
            );

            candleAggregator.Add(firstTick);
            candleAggregator.Add(secondTick);
            candleAggregator.Add(thirdTick);

            var candles = candleStore.GetCandles("EURUSD");

            candles.Should().ContainSingle();

            candles[0].Open.Should().Be(1.0849m);
            candles[0].High.Should().Be(1.0853m);
            candles[0].Low.Should().Be(1.0845m);
            candles[0].Close.Should().Be(1.0845m);
        }

        [Fact]
        public void Add_WhenMoreThanFiveHundredCandlesAreCreated_KeepsLatestFiveHundredPlusCurrent()
        {
            var candleStore = new MarketCandleStore();
            var candleAggregator = new CandleAggregator(candleStore);

            var startTime = new DateTimeOffset(
                2026, 9, 25,
                10, 0, 0,
                TimeSpan.Zero);

            for (var i = 0; i < 501; i++)
            {
                var tick = new PriceTick
                (
                    Symbol : "EURUSD",
                    Bid : 1.0848m,
                    Ask : 1.0850m,
                    Timestamp : startTime.AddSeconds(i * 5)
                );

                candleAggregator.Add(tick);
            }

            var candles = candleStore.GetCandles("EURUSD");

            candles.Should().HaveCount(501);
        }
    }
}
