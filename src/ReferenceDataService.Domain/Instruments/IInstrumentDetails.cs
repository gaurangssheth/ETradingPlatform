using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Instruments
{
    public interface IInstrumentDetails
    {
        Guid InstrumentId { get; }
    }
}
