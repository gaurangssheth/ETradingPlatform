using FluentAssertions;
using ReferenceDataService.Domain.Calculations;
using ReferenceDataService.Domain.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Tests.Calculations
{
    public class FxNotionalCalculatorTests
    {
        [Fact]
        public void Calculate_WithBaseCurrencyQuantityAndPrice_ShouldReturnQuoteCurrencyNotional()
        {
            var calculation = new FxNotionalCalculator();

            var instrument = new Instrument(
                Guid.NewGuid(),
                symbol: "EURUSD",
                assetClass: AssetClass.Fx,
                isTradable: true);

            var notional = calculation.Calculate(
                instrument,
                quantity: 100_000m,
                price: 1.0850m
            );

            notional.Should().Be(108_500m);
        }
    }
}
