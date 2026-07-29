using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Calculations;
using TradeCaptureService.Domain;
using TradeCaptureService.ReferenceData;

namespace TradeCaptureService.Calculations
{
    public sealed class BondNotionalCalculator : INotionalCalculator
    {
        private const decimal PriceParBasis = 100m;

        public AssetClass AssetClass => AssetClass.FixedIncome;

        public decimal Calculate(
            InstrumentReferenceDefinition instrumentDefinition,
            decimal quantity,
            decimal price)
        {
            ArgumentNullException.ThrowIfNull(instrumentDefinition);

            if (instrumentDefinition.Instrument.AssetClass
            != AssetClass.FixedIncome)
            {
                throw new ArgumentException(
                    "Bond notional calculator requires a fixed-income instrument.",
                    nameof(instrumentDefinition));
            }

            if (instrumentDefinition.Details
                is not BondInstrumentReferenceDetails)
            {
                throw new ArgumentException(
                    "Bond notional calculator requires bond instrument details.",
                    nameof(instrumentDefinition));
            }

            quantity = Guard.ArgumentZeroOrNegative(quantity, nameof(quantity), "Quantity must be greater than zero.");
            price = Guard.ArgumentZeroOrNegative(price, nameof(price), "Price must be greater than zero.");

            return quantity * price / PriceParBasis;
        }
    }
}
