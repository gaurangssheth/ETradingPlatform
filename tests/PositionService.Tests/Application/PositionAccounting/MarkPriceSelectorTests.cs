using FluentAssertions;
using PositionService.Application.PositionAccounting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Tests.Application.PositionAccounting
{
    public class MarkPriceSelectorTests
    {
        [Theory]
        [InlineData(100000, 1.0848, 1.0850, 1.0848)]
        [InlineData(-100000, 1.0848, 1.0850, 1.0850)]
        public void GetMarkPrice_ShouldReturnExpectedPrice(
            double netQuantity,
            double bid,
            double ask,
            double expected)
        {
            var selector = new MarkPriceSelector();

            var result = selector.GetMarkPrice(
                    (decimal)netQuantity,
                    (decimal)bid,
                    (decimal)ask);

            result.Should().Be((decimal)expected);
        }

        [Fact]
        public void GetMarkPrice_WhenPositionIsFlat_ShouldThrow()
        {
            var selector =
                new MarkPriceSelector();

            Action action = () =>
                selector.GetMarkPrice(
                    0m,
                    1.0848m,
                    1.0850m);

            action.Should()
                .Throw<InvalidOperationException>();
        }
    }
}
