using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCaptureService.ReferenceData
{
    public interface IReferenceDataClient
    {
        Task<InstrumentReferenceDefinition> GetInstrumentAsync(
            string symbol,
            string? correlationId = null,
            CancellationToken cancellationToken = default);
    }
}
