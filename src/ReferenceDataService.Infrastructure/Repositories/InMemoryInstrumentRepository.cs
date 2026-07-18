using ReferenceDataService.Domain.Instruments;

namespace ReferenceDataService.Infrastructure.Repositories
{
    public sealed class InMemoryInstrumentRepository : IInstrumentRepository
    {
        private readonly Dictionary<string, InstrumentDefinition> instruments;

        public InMemoryInstrumentRepository()
        {
            var eurUsdId = Guid.NewGuid();
            var appleId = Guid.NewGuid();
            var bondId = Guid.NewGuid();

            instruments = new Dictionary<string, InstrumentDefinition>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["EURUSD"] = new InstrumentDefinition(
                        new Instrument(
                            eurUsdId,
                            "EURUSD",
                            AssetClass.Fx,
                            isTradable: true
                        ),
                        details: new FxInstrumentDetails(
                            eurUsdId,
                            "EUR",
                            "USD",
                            0.0001m
                        )
                    ),
                ["AAPL"] = new InstrumentDefinition(
                        new Instrument(
                            appleId,
                            "AAPL",
                            AssetClass.Equity,
                            isTradable: true
                        ),
                        details: new EquityInstrumentDetails(
                            appleId,
                            "NASDAQ",
                            "USD"
                        )
                    ),
                ["GB00TEST1234"] = new InstrumentDefinition(
                        new Instrument(
                            bondId,
                            "GB00TEST1234",
                            AssetClass.FixedIncome,
                            isTradable: true
                        ),
                        details: new BondInstrumentDetails(
                            bondId,
                            isin: "GB00TEST1234",
                            issuer: "UK Government",
                            couponRate: 4.25m,
                            maturityDate: new DateOnly(2035, 6, 30),
                            parValue: 100m,
                            dayCountConvention: DayCountConvention.ActualActual
                        )
                    )
            };
        }
        public InstrumentDefinition? GetBySymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return null;
            }

            return instruments.TryGetValue(
                symbol.Trim(),
                out var instrument)
                    ? instrument
                    : null;
        }
    }
}
