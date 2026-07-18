using ReferenceDataService.Domain.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Calculations
{
    public interface INotionalCalculator
    {
        AssetClass AssetClass { get; }

        decimal Calculate(
            Instrument instrument,
            decimal quantity,
            decimal price);
    }
}
