using FluentAssertions;
using PositionService.Application.PositionAccounting;

namespace PositionService.Tests;

public class PositionCalculatorTests
{
    private readonly PositionCalculator calculator = new();

    [Fact]
    public void ApplyTrade_WhenNoExistingPositionAndBuy_ShouldOpenLong()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: 0m,
            existingAveragePrice: 0m,
            tradeSignedQuantity: 100m,
            tradePrice: 1.0800m);

        result.NewNetQuantity.Should().Be(100m);
        result.NewAveragePrice.Should().Be(1.0800m);
        result.RealisedPnl.Should().Be(0m);
    }

    [Fact]
    public void ApplyTrade_WhenNoExistingPositionAndSell_ShouldOpenShort()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: 0m,
            existingAveragePrice: 0m,
            tradeSignedQuantity: -100m,
            tradePrice: 1.0900m);

        result.NewNetQuantity.Should().Be(-100m);
        result.NewAveragePrice.Should().Be(1.0900m);
        result.RealisedPnl.Should().Be(0m);
    }

    [Fact]
    public void ApplyTrade_WhenAddingToLong_ShouldIncreaseQuantityAndRecalculateAveragePrice()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: 100m,
            existingAveragePrice: 1.0800m,
            tradeSignedQuantity: 100m,
            tradePrice: 1.1000m);

        result.NewNetQuantity.Should().Be(200m);
        result.NewAveragePrice.Should().Be(1.0900m);
        result.RealisedPnl.Should().Be(0m);
    }

    [Fact]
    public void ApplyTrade_WhenAddingToShort_ShouldIncreaseShortQuantityAndRecalculateAveragePrice()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: -100m,
            existingAveragePrice: 1.1000m,
            tradeSignedQuantity: -100m,
            tradePrice: 1.0800m);

        result.NewNetQuantity.Should().Be(-200m);
        result.NewAveragePrice.Should().Be(1.0900m);
        result.RealisedPnl.Should().Be(0m);
    }

    [Fact]
    public void ApplyTrade_WhenReducingLongWithProfit_ShouldKeepAveragePriceAndRealiseProfit()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: 100m,
            existingAveragePrice: 1.0800m,
            tradeSignedQuantity: -40m,
            tradePrice: 1.0900m);

        result.NewNetQuantity.Should().Be(60m);
        result.NewAveragePrice.Should().Be(1.0800m);
        result.RealisedPnl.Should().Be(0.4000m);
    }

    [Fact]
    public void ApplyTrade_WhenReducingLongWithLoss_ShouldKeepAveragePriceAndRealiseLoss()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: 100m,
            existingAveragePrice: 1.0800m,
            tradeSignedQuantity: -40m,
            tradePrice: 1.0700m);

        result.NewNetQuantity.Should().Be(60m);
        result.NewAveragePrice.Should().Be(1.0800m);
        result.RealisedPnl.Should().Be(-0.4000m);
    }

    [Fact]
    public void ApplyTrade_WhenClosingLong_ShouldSetQuantityAndAveragePriceToZero()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: 100m,
            existingAveragePrice: 1.0800m,
            tradeSignedQuantity: -100m,
            tradePrice: 1.0900m);

        result.NewNetQuantity.Should().Be(0m);
        result.NewAveragePrice.Should().Be(0m);
        result.RealisedPnl.Should().Be(1.0000m);
    }

    [Fact]
    public void ApplyTrade_WhenFlippingLongToShort_ShouldRealisePnlOnClosedQuantityAndUseTradePriceAsNewAverage()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: 100m,
            existingAveragePrice: 1.0800m,
            tradeSignedQuantity: -150m,
            tradePrice: 1.0900m);

        result.NewNetQuantity.Should().Be(-50m);
        result.NewAveragePrice.Should().Be(1.0900m);
        result.RealisedPnl.Should().Be(1.0000m);
    }

    [Fact]
    public void ApplyTrade_WhenReducingShortWithProfit_ShouldKeepAveragePriceAndRealiseProfit()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: -100m,
            existingAveragePrice: 1.0900m,
            tradeSignedQuantity: 40m,
            tradePrice: 1.0800m);

        result.NewNetQuantity.Should().Be(-60m);
        result.NewAveragePrice.Should().Be(1.0900m);
        result.RealisedPnl.Should().Be(0.4000m);
    }

    [Fact]
    public void ApplyTrade_WhenReducingShortWithLoss_ShouldKeepAveragePriceAndRealiseLoss()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: -100m,
            existingAveragePrice: 1.0900m,
            tradeSignedQuantity: 40m,
            tradePrice: 1.1000m);

        result.NewNetQuantity.Should().Be(-60m);
        result.NewAveragePrice.Should().Be(1.0900m);
        result.RealisedPnl.Should().Be(-0.4000m);
    }

    [Fact]
    public void ApplyTrade_WhenClosingShort_ShouldSetQuantityAndAveragePriceToZero()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: -100m,
            existingAveragePrice: 1.0900m,
            tradeSignedQuantity: 100m,
            tradePrice: 1.0800m);

        result.NewNetQuantity.Should().Be(0m);
        result.NewAveragePrice.Should().Be(0m);
        result.RealisedPnl.Should().Be(1.0000m);
    }

    [Fact]
    public void ApplyTrade_WhenFlippingShortToLong_ShouldRealisePnlOnClosedQuantityAndUseTradePriceAsNewAverage()
    {
        var result = calculator.ApplyTrade(
            existingNetQuantity: -100m,
            existingAveragePrice: 1.0900m,
            tradeSignedQuantity: 150m,
            tradePrice: 1.0800m);

        result.NewNetQuantity.Should().Be(50m);
        result.NewAveragePrice.Should().Be(1.0800m);
        result.RealisedPnl.Should().Be(1.0000m);
    }

    [Fact]
    public void ApplyTrade_WhenTradeQuantityIsZero_ShouldThrowException()
    {
        var action = () => calculator.ApplyTrade(
            existingNetQuantity: 100m,
            existingAveragePrice: 1.0800m,
            tradeSignedQuantity: 0m,
            tradePrice: 1.0900m);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Trade quantity cannot be zero.*");
    }
}