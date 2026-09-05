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
    public class UnrealisedPnlCalculatorResolverTests
    {
        private readonly UnrealisedPnlCalculatorResolver resolver;

        public UnrealisedPnlCalculatorResolverTests()
        {
            resolver = new UnrealisedPnlCalculatorResolver(
                new IUnrealisedPnlCalculator[]
                {
                    new FxUnrealisedPnlCalculator(),
                    new EquityUnrealisedPnlCalculator(),
                    new BondUnrealisedPnlCalculator()
                });
        }

        [Fact]
        public void Resolve_WhenFx_ShouldReturnFxCalculator()
        {
            var calculator = resolver.Resolve(AssetClass.Fx);

            calculator.Should().BeOfType<FxUnrealisedPnlCalculator>();
        }

        [Fact]
        public void Resolve_WhenEquity_ShouldReturnEquityCalculator()
        {
            var calculator = resolver.Resolve(AssetClass.Equity);

            calculator.Should().BeOfType<EquityUnrealisedPnlCalculator>();
        }

        [Fact]
        public void Resolve_WhenFixedIncome_ShouldReturnBondCalculator()
        {
            var calculator = resolver.Resolve(AssetClass.FixedIncome);

            calculator.Should().BeOfType<BondUnrealisedPnlCalculator>();
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
