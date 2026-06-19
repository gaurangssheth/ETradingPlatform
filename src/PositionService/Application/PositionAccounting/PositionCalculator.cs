namespace PositionService.Application.PositionAccounting
{
    public sealed class PositionCalculator
    {
        public PositionCalculationResult ApplyTrade(
            decimal existingNetQuantity,
            decimal existingAveragePrice,
            decimal tradeSignedQuantity,
            decimal tradePrice)
        {
            if (tradeSignedQuantity == 0)
            {
                throw new ArgumentException("Trade quantity cannot be zero.", nameof(tradeSignedQuantity));
            }

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

            var realisedPnl = existingNetQuantity > 0
                ? closedQuantity * (tradePrice - existingAveragePrice)
                : closedQuantity * (existingAveragePrice - tradePrice);

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