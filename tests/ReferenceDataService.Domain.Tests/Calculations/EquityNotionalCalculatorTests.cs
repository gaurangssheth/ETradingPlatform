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
    public class EquityNotionalCalculatorTests
    {
        [Fact]
        public void Calculate_WithShareQuantityAndPrice_ShouldReturnTradeNotional()
        {
            var calculation = new EquityNotionalCalculator();

            var instrument = new Instrument(
                Guid.NewGuid(),
                symbol: "AAPL",
                assetClass: AssetClass.Equity,
                isTradable: true);

            var notional = calculation.Calculate(
                instrument,
                quantity: 100m,
                price: 210.50m
            );

            notional.Should().Be(21_050m);
        }
    }
}
