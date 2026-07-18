using FluentAssertions;
using ReferenceDataService.Domain.Common;
using ReferenceDataService.Domain.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Tests.Instruments
{
    public class EquityInstrumentDetailsTests
    {
        [Fact]
        public void Create_WithValidValues_ShouldCreateEquityInstrumentDetails()
        {
            var instrumentId = Guid.NewGuid();

            var details = new EquityInstrumentDetails(
                instrumentId,
                exchange: "NASDAQ",
                tradingCurrency: "USD");

            details.InstrumentId.Should().Be(instrumentId);
            details.Exchange.Should().Be("NASDAQ");
            details.TradingCurrency.Should().Be(new CurrencyCode("USD"));
        }
    }
}
