using ReferenceDataService.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Instruments
{
    public sealed class FxInstrumentDetails : IInstrumentDetails
    {
        public FxInstrumentDetails(
            Guid instrumentId, 
            string baseCurrency, 
            string quoteCurrency, 
            decimal pipSize)
        {
            InstrumentId = Guard.ArgumentEmpty(instrumentId, nameof(instrumentId), "Instrument ID cannot be empty.");
            BaseCurrency = new CurrencyCode(baseCurrency);
            QuoteCurrency = new CurrencyCode(quoteCurrency);
            PipSize = Guard.ArgumentZeroOrNegative(pipSize, nameof(pipSize), "Pip size must be greater than zero.");
        }
        
        public Guid InstrumentId { get; }
        public CurrencyCode BaseCurrency { get; }
        public CurrencyCode QuoteCurrency { get; }
        public decimal PipSize { get; }

    }
}
