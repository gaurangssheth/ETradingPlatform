using ReferenceDataService.Domain.Instruments;
using PlatformAssetClass =
    TradingApp.SharedKernel.AssetClass;
using PlatformDayCountConvention =
    TradingApp.SharedKernel.DayCountConvention;

namespace ReferenceDataService.Infrastructure.Repositories
{
    public sealed class InMemoryInstrumentRepository : IInstrumentRepository
    {
        private readonly Dictionary<string, InstrumentDefinition> instruments;

        public InMemoryInstrumentRepository()
        {
            var eurUsdId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var appleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var bondId = Guid.Parse("33333333-3333-3333-3333-333333333333");

            instruments = new Dictionary<string, InstrumentDefinition>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["EURUSD"] = new InstrumentDefinition(
                        new Instrument(
                            eurUsdId,
                            "EURUSD",
                            PlatformAssetClass.Fx,
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
                            PlatformAssetClass.Equity,
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
                            PlatformAssetClass.FixedIncome,
                            isTradable: true
                        ),
                        details: new BondInstrumentDetails(
                            bondId,
                            isin: "GB00TEST1234",
                            issuer: "UK Government",
                            denominationCurrency: "GBP",
                            couponRate: 4.25m,
                            maturityDate: new DateOnly(2035, 6, 30),
                            parValue: 100m,
                            dayCountConvention: PlatformDayCountConvention.ActualActual
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
