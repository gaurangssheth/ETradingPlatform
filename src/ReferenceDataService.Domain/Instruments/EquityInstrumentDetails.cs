using ReferenceDataService.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Instruments
{
    public sealed class EquityInstrumentDetails : IInstrumentDetails
    {
        public EquityInstrumentDetails(
            Guid instrumentId,
            string exchange,
            string tradingCurrency)
        {
            InstrumentId = Guard.ArgumentEmpty(instrumentId, nameof(instrumentId), "Instrument ID cannot be empty.");
            Exchange = Guard.ArgumentNullOrWhiteSpace(exchange, nameof(exchange), "Exchange cannot be empty or whitespace.").ToUpperInvariant();
            TradingCurrency = new CurrencyCode(tradingCurrency);
        }

        public Guid InstrumentId { get; }

        public string Exchange { get; }

        public CurrencyCode TradingCurrency { get; }
    }
}
