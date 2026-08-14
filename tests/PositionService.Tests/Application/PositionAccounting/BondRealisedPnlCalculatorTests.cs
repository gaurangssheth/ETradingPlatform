using FluentAssertions;
using PositionService.Application.PositionAccounting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Tests.Application.PositionAccounting
{
    public class BondRealisedPnlCalculatorTests
    {
        private readonly BondRealisedPnlCalculator calculator = new();

        [Fact]
        public void Calculate_WhenPriceDifferenceIsPositive_ShouldReturnProfit()
        {
            var result = calculator.Calculate(
                closedQuantity: 1_000_000m,
                priceDifference: 1.00m);

            result.Should().Be(10_000m);
        }

        [Fact]
        public void Calculate_WhenPriceDifferenceIsNegative_ShouldReturnLoss()
        {
            var result = calculator.Calculate(
                closedQuantity: 1_000_000m,
                priceDifference: -1.00m);

            result.Should().Be(-10_000m);
        }

        [Fact]
        public void Calculate_WhenClosedQuantityIsZero_ShouldThrow()
        {
            var action = () => calculator.Calculate(
                closedQuantity: 0m,
                priceDifference: 1.00m);

            action.Should()
                .Throw<ArgumentOutOfRangeException>();
        }
    }
}
