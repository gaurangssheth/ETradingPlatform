using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Calculations;
using TradeCaptureService.Domain;
using TradeCaptureService.ReferenceData;
using TradingApp.SharedKernel;

namespace TradeCaptureService.Tests.Calculations
{
    public class FxNotionalCalculatorTests
    {
        [Fact]
        public void Calculate_WithQuantityAndPrice_ShouldReturnQuoteCurrencyNotional()
        {
            var instrumentId = Guid.NewGuid();

            var instrument = new InstrumentReferenceData
            {
                InstrumentId = instrumentId,
                Symbol = "EURUSD",
                AssetClass = AssetClass.Fx,
                IsTradable = true
            };

            var details = new FxInstrumentReferenceDetails(
                instrumentId,
                "EUR",
                "USD",
                0.0001m);

            var definition =
                new InstrumentReferenceDefinition(
                    instrument,
                    details);

            var calculator = new FxNotionalCalculator();

            var notional = calculator.Calculate(
                definition,
                quantity: 100_000m,
                price: 1.0850m);

            notional.Should().Be(108_500m);
        }
    }
}
