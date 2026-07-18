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
    public class BondNotionalCalculatorTests
    {
        [Fact]
        public void Calculate_WithNominalQuantityAndPricePerHundred_ShouldReturnTradeNotional()
        {
            var calculation = new BondNotionalCalculator();

            var instrument = new Instrument(
                Guid.NewGuid(),
                symbol: "GB00TEST1234",
                assetClass: AssetClass.FixedIncome,
                isTradable: true);

            var notional = calculation.Calculate(
                instrument,
                quantity: 1_000_000m,
                price: 98.50m
            );

            notional.Should().Be(985_000m);
        }
    }
}
