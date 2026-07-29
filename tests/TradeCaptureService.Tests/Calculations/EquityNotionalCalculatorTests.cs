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
    public class EquityNotionalCalculatorTests
    {
        [Fact]
        public void Calculate_WithShareQuantityAndPrice_ShouldReturnTradeNotional()
        {
            var instrumentId = Guid.NewGuid();

            var instrument = new InstrumentReferenceData
            {
                InstrumentId = instrumentId,
                Symbol = "AAPL",
                AssetClass = AssetClass.Equity,
                IsTradable = true
            };

            var details = new EquityInstrumentReferenceDetails(
                instrumentId,
                "NASDAQ",
                "USD");

            var definition = new InstrumentReferenceDefinition(
                instrument,
                details);

            var calculator = new EquityNotionalCalculator();

            var notional = calculator.Calculate(
                definition,
                quantity: 100m,
                price: 210.50m);

            notional.Should().Be(21_050m);
        }
    }
}
