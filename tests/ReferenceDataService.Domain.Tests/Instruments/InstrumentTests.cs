using FluentAssertions;
using ReferenceDataService.Domain.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using PlatformAssetClass =
    TradingApp.SharedKernel.AssetClass;

namespace ReferenceDataService.Domain.Tests.Instruments
{
    public class InstrumentTests
    {
        [Fact]
        public void Create_WithValidValues_ShouldCreateInstrument()
        {
            var instrumentId = Guid.NewGuid();

            var instument = new Instrument(
                instrumentId,
                symbol: "EURUSD",
                assetClass: PlatformAssetClass.Fx,
                isTradable: true
            );

            instument.InstrumentId.Should().Be(instrumentId);
            instument.Symbol.Should().Be("EURUSD");
            instument.AssetClass.Should().Be(PlatformAssetClass.Fx);
            instument.IsTradable.Should().BeTrue();
        }
    }
}
