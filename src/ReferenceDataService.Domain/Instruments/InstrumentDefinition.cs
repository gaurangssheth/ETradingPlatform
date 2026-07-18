using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Instruments
{
    public sealed class InstrumentDefinition
    {
        public InstrumentDefinition(
            Instrument instrument,
            IInstrumentDetails details)
        {
            ArgumentNullException.ThrowIfNull(instrument);
            ArgumentNullException.ThrowIfNull(details);

            if (instrument.InstrumentId != details.InstrumentId)
            {
                throw new ArgumentException(
                    "Instrument and instrument details must have the same InstrumentId.",
                    nameof(details));
            }

            Instrument = instrument;
            Details = details;
        }

        public Instrument Instrument { get; }

        public IInstrumentDetails Details { get; }
    }
}
