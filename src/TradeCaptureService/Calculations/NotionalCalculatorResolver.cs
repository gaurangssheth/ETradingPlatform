using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Domain;

namespace TradeCaptureService.Calculations
{
    public sealed class NotionalCalculatorResolver
    {
        private readonly IReadOnlyDictionary<AssetClass, INotionalCalculator> calculators;
        public NotionalCalculatorResolver(IEnumerable<INotionalCalculator> calculators)
        {
            ArgumentNullException.ThrowIfNull(calculators, nameof(calculators));

            this.calculators = calculators.ToDictionary(c => c.AssetClass, c => c);
        }

        public INotionalCalculator Resolve(AssetClass assetClass)
        {
            if (!calculators.TryGetValue(assetClass, out var calculator))
            {
                throw new NotSupportedException($"No notional calculator found for asset class '{assetClass}'.");
            }
            return calculator;
        }
    }
}
