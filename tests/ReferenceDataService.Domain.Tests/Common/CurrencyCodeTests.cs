using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.SharedKernel;

namespace ReferenceDataService.Domain.Tests.Common
{
    public class CurrencyCodeTests
    {
        [Fact]
        public void Create_WithLowerCaseValue_ShouldNormaliseToUpperCase()
        {
            var currency = new CurrencyCode("gbp");

            currency.Value.Should().Be("GBP");
        }

        [Fact]
        public void TwoEqualCurrencyCodes_ShouldBeEqual()
        {
            var first = new CurrencyCode("GBP");

            var second = new CurrencyCode("gbp");

            first.Should().Be(second);
        }

        [Theory]
        [InlineData("US")]
        [InlineData("US12")]
        [InlineData("POUNDS")]
        public void Create_WithInvalidCode_ShouldThrow(string value)
        {
            var act = () => new CurrencyCode(value);

            act.Should()
                .Throw<ArgumentException>()
                .WithParameterName(nameof(value))
                .WithMessage("*exactly three letters*");
        }

        [Fact]
        public void EqualCurrencyCodes_ShouldHaveValueEquality()
        {
            var first = new CurrencyCode("GBP");
            var second = new CurrencyCode("GBP");

            first.Should().Be(second);
            (first == second).Should().BeTrue();
        }
    }
}
