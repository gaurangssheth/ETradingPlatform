using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.PositionAccounting
{
    public sealed class UnrealisedPnlCalculatorResolver
    {
        private readonly IReadOnlyDictionary<AssetClass, IUnrealisedPnlCalculator> calculators;

        public UnrealisedPnlCalculatorResolver(IEnumerable<IUnrealisedPnlCalculator> calculators)
        {
            this.calculators = calculators.ToDictionary(calculator => calculator.AssetClass);
        }

        public IUnrealisedPnlCalculator Resolve(AssetClass assetClass)
        {
            if (!this.calculators.TryGetValue(assetClass,
                    out var calculator))
            {
                throw new InvalidOperationException(
                    $"No unrealised P&L calculator registered for asset class {assetClass}.");
            }

            return calculator;
        }
    }
}
