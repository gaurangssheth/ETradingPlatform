using ReferenceDataService.Domain.Common;
using ReferenceDataService.Domain.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Calculations
{
    public sealed class EquityNotionalCalculator : INotionalCalculator
    {
        public AssetClass AssetClass => AssetClass.Equity;

        public decimal Calculate(
            Instrument instrument,
            decimal quantity,
            decimal price)
        {
            ArgumentNullException.ThrowIfNull(instrument);
            quantity = Guard.ArgumentZeroOrNegative(quantity, nameof(quantity), "Quantity must be greater than zero.");
            price = Guard.ArgumentZeroOrNegative(price, nameof(price), "Price must be greater than zero.");

            return quantity * price;
        }
    }
}
