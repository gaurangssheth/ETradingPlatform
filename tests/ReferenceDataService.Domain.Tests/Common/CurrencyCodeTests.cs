using FluentAssertions;
using ReferenceDataService.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
