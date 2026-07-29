using FluentAssertions;
using ReferenceDataService.Domain.Instruments;
using ReferenceDataService.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlatformAssetClass =
    TradingApp.SharedKernel.AssetClass;

namespace ReferenceDataService.Infrastructure.Tests.Repositories
{
    public class InMemoryInstrumentRepositoryTests
    {
        [Fact]
        public void GetBySymbol_WhenSymbolIsFx_ShouldReturnFxDefinition()
        {
            var repository = new InMemoryInstrumentRepository();

            var definition = repository.GetBySymbol("eurusd");

            definition.Should().NotBeNull();
            definition!.Instrument.Symbol.Should().Be("EURUSD");
            definition.Instrument.AssetClass.Should().Be(PlatformAssetClass.Fx);
            definition.Details.Should().BeOfType<FxInstrumentDetails>();
        }

        [Fact]
        public void GetBySymbol_WhenSymbolIsEquity_ShouldReturnEquityDefinition()
        {
            var repository = new InMemoryInstrumentRepository();

            var definition = repository.GetBySymbol("AAPL");

            definition.Should().NotBeNull();
            definition!.Instrument.AssetClass.Should().Be(PlatformAssetClass.Equity);
            definition.Details.Should().BeOfType<EquityInstrumentDetails>();
        }

        [Fact]
        public void GetBySymbol_WhenSymbolIsFixedIncome_ShouldReturnBondDefinition()
        {
            var repository = new InMemoryInstrumentRepository();

            var definition = repository.GetBySymbol("GB00TEST1234");

            definition.Should().NotBeNull();
            definition!.Instrument.AssetClass.Should().Be(PlatformAssetClass.FixedIncome);
            definition.Details.Should().BeOfType<BondInstrumentDetails>();
        }

        [Fact]
        public void GetBySymbol_WhenSymbolDoesNotExist_ShouldReturnNull()
        {
            var repository = new InMemoryInstrumentRepository();

            var definition = repository.GetBySymbol("UNKNOWN");

            definition.Should().BeNull();
        }
    }
}
