using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCaptureService.ReferenceData
{
    public interface IInstrumentReferenceDetails
    {
        Guid InstrumentId { get; }

        CurrencyCode NotionalCurrency { get; }
    }
}
