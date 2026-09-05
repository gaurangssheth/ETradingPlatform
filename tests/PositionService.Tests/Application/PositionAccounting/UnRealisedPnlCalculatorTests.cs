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
    public class UnRealisedPnlCalculatorTests
    {
        [Theory]
        [InlineData(AssetClass.Fx, 100000, 1.0850, 1.0870, 200)]
        [InlineData(AssetClass.Fx, -100000, 1.0850, 1.0830, 200)]
        [InlineData(AssetClass.Equity, 10, 200.00, 205.00, 50)]
        [InlineData(AssetClass.FixedIncome, 100000, 98.50, 98.70, 200)]
        public void Calculate_ShouldReturnExpectedUnrealisedPnl(
            AssetClass assetClass,
            double netQuantity,
            double averagePrice,
            double markPrice,
            double expectedPnl)
        {
            IUnrealisedPnlCalculator calculator =
                assetClass switch
                {
                    AssetClass.Fx =>
                        new FxUnrealisedPnlCalculator(),

                    AssetClass.Equity =>
                        new EquityUnrealisedPnlCalculator(),

                    AssetClass.FixedIncome =>
                        new BondUnrealisedPnlCalculator(),

                    _ => throw new ArgumentOutOfRangeException(
                        nameof(assetClass))
                };

            var result =
                calculator.Calculate(
                    (decimal)netQuantity,
                    (decimal)averagePrice,
                    (decimal)markPrice);

            result.Should().Be((decimal)expectedPnl);
        }
    }
}
