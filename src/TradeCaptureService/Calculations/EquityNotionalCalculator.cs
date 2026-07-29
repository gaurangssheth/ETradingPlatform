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
    public sealed class EquityNotionalCalculator : INotionalCalculator
    {
        public AssetClass AssetClass => AssetClass.Equity;

        public decimal Calculate(
            InstrumentReferenceDefinition instrumentDefinition,
            decimal quantity,
            decimal price)
        {
            ArgumentNullException.ThrowIfNull(instrumentDefinition);

            if (instrumentDefinition.Instrument.AssetClass
            != AssetClass.Equity)
            {
                throw new ArgumentException(
                    "Equity notional calculator requires an equity instrument.",
                    nameof(instrumentDefinition));
            }

            if (instrumentDefinition.Details
            is not EquityInstrumentReferenceDetails)
            {
                throw new ArgumentException(
                    "Equity notional calculator requires equity instrument details.",
                    nameof(instrumentDefinition));
            }
            quantity = Guard.ArgumentZeroOrNegative(quantity, nameof(quantity), "Quantity must be greater than zero.");
            price = Guard.ArgumentZeroOrNegative(price, nameof(price), "Price must be greater than zero.");

            return quantity * price;
        }
    }
}
