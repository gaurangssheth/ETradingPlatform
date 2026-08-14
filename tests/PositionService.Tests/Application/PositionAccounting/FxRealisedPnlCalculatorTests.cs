using FluentAssertions;
using PositionService.Application.PositionAccounting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Tests.Application.PositionAccounting
{
    public class FxRealisedPnlCalculatorTests
    {
        private readonly FxRealisedPnlCalculator calculator = new();

        [Fact]
        public void Calculate_WhenPriceDifferenceIsPositive_ShouldReturnProfit()
        {
            var result = calculator.Calculate(
                closedQuantity: 40m,
                priceDifference: 0.0100m);

            result.Should().Be(0.4000m);
        }

        [Fact]
        public void Calculate_WhenPriceDifferenceIsNegative_ShouldReturnLoss()
        {
            var result = calculator.Calculate(
                closedQuantity: 40m,
                priceDifference: -0.0100m);

            result.Should().Be(-0.4000m);
        }

        [Fact]
        public void Calculate_WhenClosedQuantityIsZero_ShouldThrow()
        {
            var action = () => calculator.Calculate(
                closedQuantity: 0m,
                priceDifference: 0.0100m);

            action.Should()
                .Throw<ArgumentOutOfRangeException>();
        }
    }
}
