using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCaptureService.ReferenceData
{
    public sealed class InstrumentReferenceDefinition
    {
        public InstrumentReferenceDefinition(
            InstrumentReferenceData instrument,
            IInstrumentReferenceDetails details)
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

        public InstrumentReferenceData Instrument { get; }

        public IInstrumentReferenceDetails Details { get; }
    }
}
