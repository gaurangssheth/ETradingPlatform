using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Calculations;
using TradeCaptureService.Domain;
using TradingApp.SharedKernel;

namespace TradeCaptureService.Tests.Calculations
{
    public class NotionalCalculatorResolverTests
    {
        [Theory]
        [InlineData(AssetClass.Fx, typeof(FxNotionalCalculator))]
        [InlineData(AssetClass.Equity, typeof(EquityNotionalCalculator))]
        [InlineData(AssetClass.FixedIncome, typeof(BondNotionalCalculator))]
        public void Resolve_WithSupportedAssetClass_ShouldReturnCorrectCalculator(
            AssetClass assetClass, Type expectedCalculatorType)
        {
            var resolver = CreateResolver();

            var calculator = resolver.Resolve(assetClass);
            calculator.Should().BeOfType(expectedCalculatorType);
        }

        [Fact]
        public void Resolve_WhenCalculatorIsNotRegistered_ShouldThrowNotSupportedException()
        {
            var resolver = CreateResolver();

            Func<INotionalCalculator> action = () => resolver.Resolve((AssetClass)999); // Using an unsupported AssetClass value

            action.Should()
                .Throw<NotSupportedException>()
                .WithMessage(
                    "No notional calculator found for asset class '999'.");
        }

        private static NotionalCalculatorResolver CreateResolver()
        {
            return new NotionalCalculatorResolver(
                new INotionalCalculator[]
                {
                new FxNotionalCalculator(),
                new EquityNotionalCalculator(),
                new BondNotionalCalculator()
                });
        }
    }
}
