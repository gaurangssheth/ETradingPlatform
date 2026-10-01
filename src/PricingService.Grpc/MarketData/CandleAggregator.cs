using TradingApp.MarketData.Contracts;

namespace PricingService.Grpc.MarketData
{
    public sealed class CandleAggregator
    {
        private static readonly TimeSpan CandleDuration = TimeSpan.FromSeconds(5);
        private readonly MarketCandleStore candleStore;

        public CandleAggregator(MarketCandleStore candleStore)
        {
            this.candleStore = candleStore;
        }

        public MarketCandle Add(PriceTick tick)
        {
            var price = (tick.Bid + tick.Ask) / 2m;

            var candleStartTime = new DateTimeOffset(
                tick.Timestamp.Ticks -
                (tick.Timestamp.Ticks % CandleDuration.Ticks),
                tick.Timestamp.Offset);

            var candle = this.candleStore.GetCurrent(tick.Symbol);

            if (candle is null)
            {
                candle = new MarketCandle(
                    tick.Symbol,
                    candleStartTime,
                    price,
                    price,
                    price,
                    price);

                this.candleStore.SaveCurrent(candle);

                return candle;
            }

            if (candle.StartTime != candleStartTime)
            {
                this.candleStore.Complete(candle);

                var newCandle = new MarketCandle(
                    tick.Symbol,
                    candleStartTime,
                    price,
                    price,
                    price,
                    price);

                this.candleStore.SaveCurrent(newCandle);

                return newCandle;
            }

            var updatedCandle = candle with
            {
                High = Math.Max(candle.High, price),
                Low = Math.Min(candle.Low, price),
                Close = price
            };

            this.candleStore.SaveCurrent(updatedCandle);

            return updatedCandle;
        }
    }
}
