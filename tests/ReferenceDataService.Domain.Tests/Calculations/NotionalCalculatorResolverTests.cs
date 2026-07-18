using FluentAssertions;
using ReferenceDataService.Domain.Calculations;
using ReferenceDataService.Domain.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Tests.Calculations
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
            var resolver = new NotionalCalculatorResolver(
                new INotionalCalculator[]
                {
                    new FxNotionalCalculator(),
                    new EquityNotionalCalculator(),
                    new BondNotionalCalculator()
                }
            );

            var calculator = resolver.Resolve(assetClass);
            calculator.Should().BeOfType(expectedCalculatorType);
        }

        [Fact]
        public void Resolve_WhenCalculatorIsNotRegistered_ShouldThrowNotSupportedException()
        {
            var resolver = new NotionalCalculatorResolver(
                new INotionalCalculator[]
                {
                    new FxNotionalCalculator(),
                    new EquityNotionalCalculator(),
                    new BondNotionalCalculator()
                });

            Func<INotionalCalculator> action = () => resolver.Resolve((AssetClass)999); // Using an unsupported AssetClass value

            action.Should()
                .Throw<NotSupportedException>()
                .WithMessage(
                    "No notional calculator found for asset class '999'.");


        }
    }
}
