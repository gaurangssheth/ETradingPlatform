namespace PositionService.Application.PositionAccounting
{
    public sealed class PositionCalculator
    {
        private readonly RealisedPnlCalculatorResolver realisedPnlCalculatorResolver;

        public PositionCalculator(RealisedPnlCalculatorResolver realisedPnlCalculatorResolver)
        {
            this.realisedPnlCalculatorResolver = realisedPnlCalculatorResolver;
        }

        public PositionCalculationResult ApplyTrade(
            AssetClass assetClass,
            decimal existingNetQuantity,
            decimal existingAveragePrice,
            decimal tradeSignedQuantity,
            decimal tradePrice)
        {
            if (tradeSignedQuantity == 0)
            {
                throw new ArgumentException("Trade quantity cannot be zero.", nameof(tradeSignedQuantity));
            }

            var realisedPnlCalculator = realisedPnlCalculatorResolver.Resolve(assetClass);

            if (existingNetQuantity == 0)
            {
                return new PositionCalculationResult
                {
                    NewNetQuantity = tradeSignedQuantity,
                    NewAveragePrice = tradePrice,
                    RealisedPnl = 0m
                };
            }
                        
            var sameDirection =
                Math.Sign(existingNetQuantity) == Math.Sign(tradeSignedQuantity);

            if (sameDirection)
            {
                var existingAbsQuantity = Math.Abs(existingNetQuantity);
                var tradeAbsQuantity = Math.Abs(tradeSignedQuantity);
                var newNetQuantity = existingNetQuantity + tradeSignedQuantity;

                var newAveragePrice =
                    ((existingAbsQuantity * existingAveragePrice) +
                     (tradeAbsQuantity * tradePrice))
                    / Math.Abs(newNetQuantity);

                return new PositionCalculationResult
                {
                    NewNetQuantity = newNetQuantity,
                    NewAveragePrice = newAveragePrice,
                    RealisedPnl = 0m
                };
            }

            var existingAbs = Math.Abs(existingNetQuantity);
            var tradeAbs = Math.Abs(tradeSignedQuantity);
            var closedQuantity = Math.Min(existingAbs, tradeAbs);

            var priceDifference = existingNetQuantity > 0
                ? tradePrice - existingAveragePrice
                : existingAveragePrice - tradePrice;

            var realisedPnl = realisedPnlCalculator.Calculate(
                closedQuantity,
                priceDifference);

            var resultingNetQuantity = existingNetQuantity + tradeSignedQuantity;

            if (resultingNetQuantity == 0)
            {
                return new PositionCalculationResult
                {
                    NewNetQuantity = 0m,
                    NewAveragePrice = 0m,
                    RealisedPnl = realisedPnl
                };
            }

            var flipped = Math.Sign(resultingNetQuantity) != Math.Sign(existingNetQuantity);

            if (flipped)
            {
                return new PositionCalculationResult
                {
                    NewNetQuantity = resultingNetQuantity,
                    NewAveragePrice = tradePrice,
                    RealisedPnl = realisedPnl
                };
            }

            return new PositionCalculationResult
            {
                NewNetQuantity = resultingNetQuantity,
                NewAveragePrice = existingAveragePrice,
                RealisedPnl = realisedPnl
            };
        }
    }
}