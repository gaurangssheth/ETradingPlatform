using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Instruments
{
    public class Instrument
    {
        public Instrument(Guid instrumentId, string symbol, AssetClass assetClass, bool isTradable)
        {
            ArgumentNullException.ThrowIfNull(symbol, nameof(symbol));
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("Symbol cannot be empty or whitespace.", nameof(symbol));
            }

            InstrumentId = instrumentId;
            Symbol = symbol;
            AssetClass = assetClass;
            IsTradable = isTradable;
        }

        public Guid InstrumentId { get; }

        public string Symbol { get; }

        public AssetClass AssetClass { get; }

        public bool IsTradable { get; }
    }
}
