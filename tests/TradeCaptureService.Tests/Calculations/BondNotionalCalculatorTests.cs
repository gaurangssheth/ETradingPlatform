using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Calculations;
using TradeCaptureService.Domain;
using TradeCaptureService.ReferenceData;
using TradingApp.SharedKernel;

namespace TradeCaptureService.Tests.Calculations
{
    public class BondNotionalCalculatorTests
    {
        [Fact]
        public void Calculate_WithNominalQuantityAndPricePerHundred_ShouldReturnTradeNotional()
        {
            var instrumentId = Guid.NewGuid();

            var instrument = new InstrumentReferenceData
            {
                InstrumentId = instrumentId,
                Symbol = "GB00TEST1234",
                AssetClass = AssetClass.FixedIncome,
                IsTradable = true
            };

            var details = new BondInstrumentReferenceDetails(
                instrumentId,
                isin: "GB00TEST1234",
                issuer: "UK Government",
                denominationCurrency: "GBP",
                couponRate: 4.25m,
                maturityDate: new DateOnly(2035, 6, 30),
                parValue: 100m,
                dayCountConvention:
                    DayCountConvention.ActualActual);

            var definition = new InstrumentReferenceDefinition(
                instrument,
                details);

            var calculator = new BondNotionalCalculator();

            var notional = calculator.Calculate(
                definition,
                quantity: 1_000_000m,
                price: 98.50m);

            notional.Should().Be(985_000m);
        }
    }
}
