# E-Trading Platform — Architecture Notes

_Last updated: 18 July 2026_

## 1. Purpose

This solution is being built as a realistic, extensible electronic-trading platform rather than a single-asset demo.

The platform currently demonstrates:

- external REST order submission;
- asynchronous service-to-service workflows with NServiceBus and RabbitMQ;
- SQL persistence with EF Core and the Outbox pattern;
- synchronous internal calls with gRPC;
- pre-trade risk checks;
- order lifecycle persistence and retry-safe processing;
- FX pricing and execution;
- position accounting with realised P&L;
- a multi-asset reference-data foundation for FX, equities and fixed income;
- structured logging and end-to-end correlation IDs;
- unit, service and infrastructure tests.

The long-term target includes market and limit orders, live prices, working-order evaluation, unrealised P&L, React UI, SignalR/WebSockets, ZeroMQ where justified, FIX connectivity, audit trails, and fixed-income analytics.

## 2. Current high-level architecture

```text
Client / Swagger / future React UI
                 |
                 v
        TradingGateway.Api
                 |
        SubmitOrder command
                 |
                 v
            RabbitMQ
                 |
                 v
           OrderService
        |                 |
        | gRPC            | publishes
        v                 v
   RiskService.Grpc   OrderAccepted / OrderRejected
                              |
                              v
                    TradeCaptureService
                       |             |
                       | gRPC        | gRPC
                       v             v
              PricingService.Grpc  ReferenceDataService.Grpc
                                           |
                                           v
                              ReferenceDataService.Infrastructure
                                           |
                                           v
                                ReferenceDataService.Domain

                    TradeCaptured event
                              |
                              v
                       PositionService
```

### Service responsibilities

**TradingGateway.Api** receives HTTP orders, validates basic request shape, creates `SubmitOrder`, propagates `X-Correlation-Id`, and returns `202 Accepted` because processing is asynchronous.

**OrderService** owns the order lifecycle. It persists `PendingRisk` before calling RiskService, reuses the same row during retries, updates to `Accepted` or `Rejected`, and publishes the corresponding integration event.

**RiskService.Grpc** performs synchronous pre-trade checks and returns approval or rejection with a `RiskDecisionId`. Technical failures are surfaced as gRPC exceptions.

**TradeCaptureService** currently prices and captures accepted orders. Next it will obtain instrument metadata, resolve the correct notional strategy, and persist instrument, asset-class and notional information.

**PricingService.Grpc** currently serves FX quotes. It will be extended to equity and fixed-income prices and later live pricing.

**PositionService** maintains positions and realised P&L. Unrealised P&L and asset-specific valuation come later.

**ReferenceDataService.Domain** contains pure domain types and rules. It must not depend on gRPC, EF Core, RabbitMQ, NServiceBus, SQL, Serilog or ASP.NET Core.

**ReferenceDataService.Infrastructure** contains repository implementations and later EF Core, migrations and SQL persistence.

**ReferenceDataService.Grpc** contains the `.proto`, generated gRPC classes, service, mapper, logging and correlation handling.

## 3. Order lifecycle and failure behaviour

### Successful risk path

```text
SubmitOrder received
    -> Order saved as PendingRisk
    -> RiskService approves
    -> Same row updated to Accepted
    -> OrderAccepted published
    -> TradeCapture continues
```

### Business rejection path

```text
SubmitOrder received
    -> Order saved as PendingRisk
    -> RiskService rejects
    -> Same row updated to Rejected
    -> OrderRejected published
    -> No trade is created
```

`OrderRejected` is currently an integration event without a downstream consumer. Future consumers may include an order read model, React UI, audit service, risk reporting, or FIX gateway. TradeCaptureService must not consume it.

### RiskService unavailable

```text
SubmitOrder received
    -> Order saved as PendingRisk
    -> gRPC throws Unavailable
    -> NServiceBus retries
    -> Existing PendingRisk row reused
```

Runtime testing confirmed that `PendingRisk` survives, only one row exists, and a later successful retry updates that same row to `Accepted`.

### Recoverability

Example configuration:

```text
Immediate retries = 2
Delayed retries   = 3
Delay increase    = 5 seconds
```

Total handler attempts:

```text
(1 original + 2 immediate) × (1 original cycle + 3 delayed cycles)
= 3 × 4
= 12 attempts
```

Normal risk rejection is not an exception. It is a successful gRPC response with `Approved = false`.

## 4. Multi-asset reference-data model

### Asset class versus instrument

Asset classes:

```text
FX
Equity
Fixed Income
```

Specific instruments:

```text
EURUSD
AAPL
GB00TEST1234
```

`Bond` is not an asset class; it is an instrument type inside `FixedIncome`.

### Core Instrument

```csharp
public sealed class Instrument
{
    public Guid InstrumentId { get; }
    public string Symbol { get; }
    public AssetClass AssetClass { get; }
    public bool IsTradable { get; }
}
```

### Asset-specific details

```text
Instrument + exactly one IInstrumentDetails implementation
```

Implementations:

```text
FxInstrumentDetails
EquityInstrumentDetails
BondInstrumentDetails
```

`InstrumentDefinition` composes the common instrument and one details object. This avoids adding a new nullable domain property for every future asset class.

### FX details

```text
BaseCurrency
QuoteCurrency
PipSize
```

Example: `EURUSD` has base `EUR`, quote `USD`, pip size `0.0001`.

### Equity details

```text
Exchange
TradingCurrency
```

Example: `AAPL` trades on `NASDAQ` in `USD`.

### Bond details

```text
ISIN
Issuer
CouponRate
MaturityDate
ParValue
DayCountConvention
```

These will be expanded gradually through real calculations: clean price, dirty price, accrued interest, settlement, yield, duration and coupon cash flows.

## 5. Notional calculations

### Equity

```text
100 shares × 210.50 USD = 21,050 USD
```

### FX

```text
100,000 EUR × 1.0850 USD/EUR = 108,500 USD
```

### Fixed income

Bond prices are commonly quoted per 100 nominal:

```text
1,000,000 × 98.50 / 100 = 985,000
```

This is currently clean-price trade value only.

## 6. Why this is a Strategy Pattern

Current pieces:

```text
INotionalCalculator
    |-- FxNotionalCalculator
    |-- EquityNotionalCalculator
    |-- BondNotionalCalculator

NotionalCalculatorResolver
```

### Clues that identify Strategy

1. One business action has several interchangeable algorithms.
2. The algorithms share one interface.
3. The caller does not contain one large `if` or `switch` with every algorithm.
4. A strategy is selected at runtime.
5. The selected object performs the behaviour.

Memory aid:

> Same business action, different algorithm, selected at runtime.

For this platform:

> Same action: calculate notional. Different algorithms: FX, equity and fixed income. Selected at runtime from the instrument's asset class.

Target runtime use:

```csharp
var calculator =
    calculatorResolver.Resolve(instrument.AssetClass);

var notional = calculator.Calculate(
    instrument,
    quantity,
    executionPrice);
```

Merely having an interface, classes and resolver is only the foundation. It becomes a live Strategy implementation when TradeCaptureService uses them during the real order-to-trade workflow.

## 7. Protobuf field numbers

```proto
message GetInstrumentResponse {
  string instrumentId = 1;
  string symbol = 2;
  AssetClass assetClass = 3;
  bool isTradable = 4;

  oneof details {
    FxInstrumentDetails fxDetails = 5;
    EquityInstrumentDetails equityDetails = 6;
    BondInstrumentDetails bondDetails = 7;
  }
}
```

The numbers are protobuf **field tags**. They are stable wire identifiers, not array positions.

```proto
bool isTradable = 4;
```

means:

```text
Source-code name = isTradable
Wire identifier  = 4
```

Protobuf serialises field number plus value, allowing generated clients in C#, Java, C++, Python, Go and other languages to understand the same message.

Rules:

- never change an existing field number;
- never reuse a removed number;
- add new fields using new numbers;
- reserve removed numbers and names.

Each `oneof` option is still a protobuf field, so each requires its own tag. For FX field `5` is active, equity field `6`, and bond field `7`.

Generated C# exposes:

```text
FxDetails
EquityDetails
BondDetails
DetailsCase
DetailsOneofCase
ClearDetails()
```

Non-active detail properties normally appear as `null`. `DetailsCase` is the authoritative discriminator.

The domain uses `IInstrumentDetails`; protobuf uses `oneof`. Protobuf cannot directly use a C# interface because it is language-neutral data, not .NET behaviour.

## 8. Correlation IDs and logging

Correlation flow:

```text
HTTP -> NServiceBus -> gRPC metadata -> Serilog LogContext -> logs
```

Service pattern:

```csharp
var correlationId = context.RequestHeaders
    .GetValue(GrpcCorrelationConstants.MetadataKey);

using (LogContext.PushProperty(
    GrpcCorrelationConstants.MetadataKey,
    correlationId))
{
    // every log in this scope is enriched
}
```

Serilog must include:

```csharp
.Enrich.FromLogContext()
```

## 9. Modern C# constructs used

### Nullable reference types

`InstrumentDefinition?` means the contract allows null. Without `?`, it should not be null.

### Null-forgiving operator

`null!` suppresses a compiler warning; it does not prevent runtime null.

### Guard clauses

`ArgumentNullException.ThrowIfNull` and the domain `Guard` helper prevent invalid objects.

### `init`

Allows assignment during construction or object initialisation but not unrestricted later mutation.

### `with`

Creates a copy of a record or suitable struct with selected values changed.

### `readonly record struct`

Used for small immutable value objects such as `CurrencyCode`. It provides value semantics and generated equality, hash code, `==`, `!=`, `ToString`, and `with` support.

### Type pattern matching

```csharp
case DomainFxDetails fxDetails:
```

checks the runtime type and declares a typed local variable.

### Switch expression

Use for concise value-to-value mappings:

```csharp
return assetClass switch
{
    AssetClass.Fx => GrpcAssetClass.AssetClassFx,
    AssetClass.Equity => GrpcAssetClass.AssetClassEquity,
    AssetClass.FixedIncome => GrpcAssetClass.AssetClassFixedIncome,
    _ => GrpcAssetClass.AssetClassUnspecified
};
```

Use a traditional `switch` when each case performs several operations.

### Index initialiser

```csharp
var instruments = new Dictionary<string, InstrumentDefinition>
{
    ["EURUSD"] = eurUsdDefinition,
    ["AAPL"] = appleDefinition
};
```

Equivalent to assigning by dictionary index.

### `IReadOnlyDictionary`

Expresses read-only access through that reference. Hashing and equality apply to the key type, not the value type.

### Delegates and delayed execution

```csharp
Func<INotionalCalculator> action =
    () => resolver.Resolve((AssetClass)999);
```

The delegate stores the call. FluentAssertions later executes it inside its exception assertion.

### `ToUpperInvariant`

Use for machine identifiers such as currency codes, symbols, ISINs and protocol values. It is independent of server culture.

### Type aliases

Useful when domain and generated protobuf classes have the same names.

## 10. Design patterns currently present

- Repository: `IInstrumentRepository` and implementations.
- Strategy foundation: calculators plus resolver.
- Mapper: `IInstrumentGrpcMapper` and `InstrumentGrpcMapper`.
- Dependency Injection: constructor-injected abstractions.
- Unit of Work and Repository in transactional services.
- Outbox with NServiceBus SQL persistence.
- Composition over inheritance: `Instrument + IInstrumentDetails`.

## 11. Current project structure

```text
src/
  TradingGateway.Api/
  OrderService/
  TradeCaptureService/
  PositionService/
  PricingService.Grpc/
  RiskService.Grpc/
  ReferenceDataService.Domain/
  ReferenceDataService.Infrastructure/
  ReferenceDataService.Grpc/
  TradingApp.Contracts/
  TradingApp.Shared/

tests/
  OrderService.Tests/
  TradeCaptureService.Tests/
  PositionService.Tests/
  PricingService.Tests/
  RiskService.Tests/
  ReferenceDataService.Domain.Tests/
  ReferenceDataService.Infrastructure.Tests/
  ReferenceDataService.Grpc.Tests/
```

All projects target .NET 8. A solution-root `global.json` should pin SDK `8.0.423` with `latestPatch` roll-forward.

## 12. Next phase: TradeCapture reference-data and multi-asset integration

Target flow:

```text
OrderAccepted
    -> TradeCaptureService
    -> ReferenceDataService.GetInstrument(symbol)
    -> PricingService.GetPrice(symbol)
    -> NotionalCalculatorResolver.Resolve(assetClass)
    -> selected calculator.Calculate(...)
    -> persist Trade with InstrumentId, AssetClass, Notional and currency
    -> publish TradeCaptured
```

Planned sequence:

1. Finalise ReferenceDataService runtime configuration.
2. Add `IReferenceDataClient` and `GrpcReferenceDataClient` to TradeCaptureService.
3. Propagate correlation metadata.
4. Map protobuf response into a TradeCapture model.
5. Register calculator strategies and resolver.
6. Use the resolver in the live handler.
7. Extend Trade and `TradeCaptured`.
8. Add fixed-income currency metadata.
9. Expand PricingService to AAPL and a bond.
10. Run end-to-end FX, equity and fixed-income tests.
11. Then implement market and limit-order intent.

## 13. Future roadmap

### Market and limit orders

```text
Market -> execute immediately using bid/ask
Limit  -> execute only when price condition is met; otherwise remain Working
```

Working orders will later justify a saga or process manager.

### Live market data

```text
Market-data source
    -> backend transport such as ZeroMQ where justified
    -> market-data/pricing service
    -> SignalR/WebSocket
    -> React UI
```

### P&L and reporting

- unrealised P&L;
- asset-specific valuation;
- FX conversion to account currency;
- position and P&L time series;
- exposure reports;
- dashboards and charts.

### FIX

- inbound new orders;
- mapping to internal `SubmitOrder`;
- outbound execution reports and rejects;
- preservation of external and internal identifiers.

## 14. Build, test and commit

```powershell
dotnet build
dotnet test
git status
git add .
git status
git commit -m "feat: add multi-asset reference data foundation"
git push
```

Suggested next-page title:

```text
Trading App Phase 2D – TradeCapture Reference Data and Multi-Asset Integration
```
