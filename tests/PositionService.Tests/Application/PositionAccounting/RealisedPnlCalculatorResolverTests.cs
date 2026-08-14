using FluentAssertions;
using PositionService.Application.PositionAccounting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.SharedKernel;

namespace PositionService.Tests.Application.PositionAccounting
{
    public class RealisedPnlCalculatorResolverTests
    {
        private readonly RealisedPnlCalculatorResolver resolver;

        public RealisedPnlCalculatorResolverTests()
        {
            resolver = new RealisedPnlCalculatorResolver(
                new IRealisedPnlCalculator[]
                {
                    new FxRealisedPnlCalculator(),
                    new EquityRealisedPnlCalculator(),
                    new BondRealisedPnlCalculator()
                });
        }

        [Fact]
        public void Resolve_WhenFx_ShouldReturnFxCalculator()
        {
            var calculator = resolver.Resolve(AssetClass.Fx);

            calculator.Should().BeOfType<FxRealisedPnlCalculator>();
        }

        [Fact]
        public void Resolve_WhenEquity_ShouldReturnEquityCalculator()
        {
            var calculator = resolver.Resolve(AssetClass.Equity);

            calculator.Should().BeOfType<EquityRealisedPnlCalculator>();
        }

        [Fact]
        public void Resolve_WhenFixedIncome_ShouldReturnBondCalculator()
        {
            var calculator = resolver.Resolve(AssetClass.FixedIncome);

            calculator.Should().BeOfType<BondRealisedPnlCalculator>();
        }

        [Fact]
        public void Resolve_WhenAssetClassIsUnsupported_ShouldThrow()
        {
            var action = () => resolver.Resolve((AssetClass)999);

            action.Should()
                .Throw<InvalidOperationException>();
        }
    }
}
