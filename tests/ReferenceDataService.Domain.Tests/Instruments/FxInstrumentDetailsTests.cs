using FluentAssertions;
using ReferenceDataService.Domain.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.SharedKernel;

namespace ReferenceDataService.Domain.Tests.Instruments
{
    public class FxInstrumentDetailsTests
    {
        [Fact]
        public void Create_WithValidValues_ShouldCreateFxInstrumentDetails()
        {
            var instrumentId = Guid.NewGuid();

            var details = new FxInstrumentDetails(
                instrumentId,
                baseCurrency: "EUR",
                quoteCurrency: "USD",
                pipSize: 0.0001m
            );

            details.InstrumentId.Should().Be(instrumentId);
            details.BaseCurrency.Should().Be(new CurrencyCode("EUR"));
            details.QuoteCurrency.Should().Be(new CurrencyCode("USD"));
            details.PipSize.Should().Be(0.0001m);
        }
    }
}
