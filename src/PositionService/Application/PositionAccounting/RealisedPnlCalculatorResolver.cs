using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PositionService.Application.PositionAccounting
{
    public sealed class RealisedPnlCalculatorResolver
    {
        private readonly IReadOnlyDictionary<AssetClass, IRealisedPnlCalculator> calculators;

        public RealisedPnlCalculatorResolver(
            IEnumerable<IRealisedPnlCalculator> calculators)
        {
            this.calculators = calculators?.ToDictionary(c => c.AssetClass) ?? throw new ArgumentNullException(nameof(calculators));
        }

        public IRealisedPnlCalculator Resolve(AssetClass assetClass)
        {
            if (!calculators.TryGetValue(assetClass, out var calculator))
            {
                throw new InvalidOperationException(
                $"No realised P&L calculator registered for asset class '{assetClass}'.");
            }

            return calculator;
        }
    }
}
