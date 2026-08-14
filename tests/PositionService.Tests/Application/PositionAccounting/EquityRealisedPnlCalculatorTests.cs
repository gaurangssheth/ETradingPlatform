using FluentAssertions;
using PositionService.Application.PositionAccounting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Tests.Application.PositionAccounting
{
    public class EquityRealisedPnlCalculatorTests
    {
        private readonly EquityRealisedPnlCalculator calculator = new();

        [Fact]
        public void Calculate_WhenPriceDifferenceIsPositive_ShouldReturnProfit()
        {
            var result = calculator.Calculate(
                closedQuantity: 40m,
                priceDifference: 10m);

            result.Should().Be(400m);
        }

        [Fact]
        public void Calculate_WhenPriceDifferenceIsNegative_ShouldReturnLoss()
        {
            var result = calculator.Calculate(
                closedQuantity: 40m,
                priceDifference: -10m);

            result.Should().Be(-400m);
        }

        [Fact]
        public void Calculate_WhenClosedQuantityIsZero_ShouldThrow()
        {
            var action = () => calculator.Calculate(
                closedQuantity: 0m,
                priceDifference: 10m);

            action.Should()
                .Throw<ArgumentOutOfRangeException>();
        }
    }
}
