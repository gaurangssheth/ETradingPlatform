using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Domain;
using TradeCaptureService.ReferenceData;

namespace TradeCaptureService.Calculations
{
    public interface INotionalCalculator
    {
        AssetClass AssetClass { get; }

        decimal Calculate(
            InstrumentReferenceDefinition instrumentDefinition,
            decimal quantity,
            decimal price);
    }
}
