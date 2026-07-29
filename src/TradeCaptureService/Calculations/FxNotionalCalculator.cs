using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Domain;
using TradeCaptureService.ReferenceData;

namespace TradeCaptureService.Calculations
{
    public sealed class FxNotionalCalculator : INotionalCalculator
    {
        public AssetClass AssetClass => AssetClass.Fx;

        public decimal Calculate(
            InstrumentReferenceDefinition instrumentDefinition, 
            decimal quantity, decimal price)
        {
            ArgumentNullException.ThrowIfNull(instrumentDefinition);

            if (!instrumentDefinition.Instrument.AssetClass.Equals(AssetClass.Fx))
            {
                throw new ArgumentException(
                    "FX notional calculator requires an FX instrument.",
                    nameof(instrumentDefinition));
            }

            if (instrumentDefinition.Details
            is not FxInstrumentReferenceDetails)
            {
                throw new ArgumentException(
                    "FX notional calculator requires FX instrument details.",
                    nameof(instrumentDefinition));
            }

            quantity = Guard.ArgumentZeroOrNegative(quantity, nameof(quantity), "Quantity must be greater than zero.");
            price = Guard.ArgumentZeroOrNegative(price, nameof(price), "Price must be greater than zero.");

            return quantity * price;

        }
    }
}
