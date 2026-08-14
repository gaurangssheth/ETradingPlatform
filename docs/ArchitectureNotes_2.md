# E-Trading Platform — Architecture Notes

_Last updated: 29 July 2026_

## 1. Purpose

This solution is being built step by step as a realistic, extensible, multi-asset electronic-trading platform rather than as a single-asset demonstration.

The platform currently demonstrates:

- external REST order submission through Swagger;
- asynchronous workflows with NServiceBus and RabbitMQ;
- SQL persistence with EF Core and NServiceBus SQL persistence/outbox;
- synchronous internal service calls with gRPC;
- pre-trade risk checks;
- retry-safe order processing and idempotency;
- executable bid/ask pricing;
- multi-asset reference data for FX, equities and fixed income;
- asset-specific trade-notional strategies selected at runtime;
- trade persistence with stable instrument identity, asset class and currency;
- position accounting with open/add/reduce/close/flip behaviour and realised P&L for the original FX/equity-style calculation;
- structured logging and end-to-end correlation IDs;
- focused domain, gRPC, handler, infrastructure and integration-style tests.

The long-term target includes market and limit orders, working orders, live prices, unrealised P&L, multi-asset realised P&L, React UI, SignalR/WebSockets, ZeroMQ where justified, FIX connectivity, audit trails, reporting, charts and fixed-income analytics.

Nothing is treated as permanently fixed at this early stage. When a model changes materially and the development data has no value, a service database and its initial migration may be rebuilt cleanly. Once data becomes valuable or production-like, schema changes must become forward, data-preserving migrations.

---

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
                    |        |          |
                    | gRPC   | gRPC     | strategy selection
                    v        v          v
          ReferenceData   Pricing   NotionalCalculatorResolver
          Service.Grpc    Service        |
              |           .Grpc          v
              v                    FX / Equity / Bond
 ReferenceDataService              calculator
      Infrastructure                    |
              |                         v
              v                   Trade persisted
 ReferenceDataService                    |
        Domain                           v
                                  TradeCaptured event
                                          |
                                          v
                                   PositionService
```

### Service responsibilities

**TradingGateway.Api** receives HTTP orders, validates request shape, creates `SubmitOrder`, propagates `X-Correlation-Id`, opens and commits an NServiceBus transactional session, and returns `202 Accepted` because processing is asynchronous.

**OrderService** owns the order lifecycle. It persists `PendingRisk` before calling RiskService, reuses the same row during retries, updates it to `Accepted` or `Rejected`, and publishes the corresponding integration event.

**RiskService.Grpc** performs synchronous pre-trade checks and returns an approval/rejection decision with a `RiskDecisionId`. Technical failures are surfaced as gRPC exceptions so NServiceBus recoverability can retry the message.

**ReferenceDataService.Grpc** returns authoritative instrument identity, common metadata and one asset-specific detail object. It currently uses an in-memory repository, but the service boundary is designed so the repository can later be replaced with SQL persistence.

**PricingService.Grpc** returns executable bid, ask and mid prices. It currently uses deterministic in-memory prices for runtime testing. Later it will consume live market-data sources.

**TradeCaptureService** now owns the complete accepted-order-to-trade flow:

1. obtain instrument reference data;
2. obtain a price quote;
3. select bid or ask as the execution price;
4. resolve the correct asset-specific notional strategy;
5. calculate trade notional;
6. persist the captured trade;
7. publish `TradeCaptured`.

**PositionService** maintains positions and movement history. Its lifecycle logic is implemented, but its realised P&L calculation and schema are not yet multi-asset safe. This is the next major phase.

**TradingApp.SharedKernel** contains deliberately shared business vocabulary and validation that has identical meaning across service boundaries:

```text
AssetClass
CurrencyCode
DayCountConvention
Guard
```

**TradingApp.Shared** remains for technical cross-cutting concerns such as correlation, messaging, connection-string names and diagnostics.

---

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

`OrderRejected` is currently an integration event without a downstream consumer. Future consumers may include an order read model, React UI, audit service, risk reporting or FIX gateway. TradeCaptureService must not consume it.

### RiskService unavailable

```text
SubmitOrder received
    -> Order saved as PendingRisk
    -> gRPC throws Unavailable
    -> NServiceBus retries
    -> Existing PendingRisk row reused
```

Runtime testing confirmed that `PendingRisk` survives, only one row exists, and a later successful retry updates the same row to `Accepted`.

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

A normal risk rejection is not an exception. It is a successful gRPC response with `Approved = false`.

### Transaction ownership

Code outside an NServiceBus handler, such as the HTTP gateway, explicitly opens and commits its transactional session.

Inside an NServiceBus handler, do not call `transactionalSession.Commit(...)`. NServiceBus owns the incoming-message transaction and completes it when the handler returns successfully. EF changes still require `SaveChangesAsync()`.

A later hardening task is to review whether application EF work and NServiceBus persistence are enlisted in the intended atomic transaction for each service. Adding an explicit commit inside a handler is not the solution.

---

## 4. Reference data and instrument modelling

### Why the name “Reference Data”

Reference data is relatively stable, authoritative information used to understand a financial instrument. It is different from market data and transaction data.

```text
Reference data  -> instrument ID, asset class, ISIN, currencies, venue, maturity
Market data     -> bid, ask, mid, yield, spread
Transaction data-> order, execution, trade, position
```

`ReferenceDataService` is intentionally broader than `InstrumentMasterService`, because it may later own currencies, trading venues, calendars and other reference data.

### Asset class versus instrument

Asset classes:

```text
Fx
Equity
FixedIncome
```

Specific instruments currently used for deterministic runtime testing:

```text
EURUSD
AAPL
GB00TEST1234
```

`Bond` is not an asset class. It is an instrument type within `FixedIncome`.

### Shared asset-class type

The platform previously accumulated separate domain and event-contract copies of `AssetClass`. These were consolidated into:

```text
TradingApp.SharedKernel.AssetClass
```

The generated protobuf enum remains separate because it is a transport type:

```text
ReferenceDataService.Grpc.AssetClass
```

Therefore, mapping is required only at the protobuf boundary.

### ReferenceData domain model

The authoritative ReferenceData domain uses:

```text
InstrumentDefinition
├── Instrument
│   ├── InstrumentId
│   ├── Symbol
│   ├── AssetClass
│   └── IsTradable
└── IInstrumentDetails
    ├── FxInstrumentDetails
    ├── EquityInstrumentDetails
    └── BondInstrumentDetails
```

Composition avoids adding an ever-growing list of nullable properties to one instrument class.

### TradeCapture reference model

TradeCapture does not reference `ReferenceDataService.Domain`, because one service must not depend on another service's internal domain assembly.

It maps the gRPC response into its own immutable snapshot:

```text
InstrumentReferenceDefinition
├── InstrumentReferenceData
│   ├── InstrumentId
│   ├── Symbol
│   ├── AssetClass
│   └── IsTradable
└── IInstrumentReferenceDetails
    ├── FxInstrumentReferenceDetails
    ├── EquityInstrumentReferenceDetails
    └── BondInstrumentReferenceDetails
```

The three detail snapshots are `sealed record` reference types because they are immutable data snapshots with useful value equality. They are not `record struct` types because they contain several fields and are passed through an interface, which would box a struct.

`InstrumentReferenceDefinition` validates that the common instrument and its details carry the same `InstrumentId`.

### FX details

```text
InstrumentId
BaseCurrency
QuoteCurrency
PipSize
```

For FX, the calculated trade notional is expressed in the quote currency:

```text
EURUSD -> USD
```

### Equity details

```text
InstrumentId
Exchange
TradingCurrency
```

For equity, the trade notional is expressed in the trading currency.

### Bond details

```text
InstrumentId
ISIN
Issuer
DenominationCurrency
CouponRate
MaturityDate
ParValue
DayCountConvention
```

`DenominationCurrency` was added because a fixed-income trade cannot persist a meaningful notional currency without it.

The new protobuf field was added as tag `7`; existing tags were not renumbered.

### Stable IDs in the current in-memory repository

The in-memory repository now uses deterministic IDs for runtime consistency:

```text
EURUSD        11111111-1111-1111-1111-111111111111
AAPL          22222222-2222-2222-2222-222222222222
GB00TEST1234  33333333-3333-3333-3333-333333333333
```

These values are temporary development data. When ReferenceDataService gets its own database, persistent instrument records will own the IDs.

Important rules:

- these sample IDs must not be used to infer or backfill unknown historical production data;
- migrations must not depend on temporary in-memory symbols;
- there is no cross-database foreign key from TradeCapture to ReferenceDataService;
- TradeCapture stores the instrument ID received at execution time as part of its historical trade record.

---

## 5. gRPC contract and protobuf `oneof`

The response contains common fields plus exactly one details payload:

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

Protobuf field numbers are stable wire identifiers, not source-code positions.

Rules:

- never change an existing field number;
- never reuse a removed field number;
- add a new field using a new unused number;
- reserve removed names and numbers.

Generated C# exposes:

```text
FxDetails
EquityDetails
BondDetails
DetailsCase
DetailsOneofCase
ClearDetails()
```

`DetailsCase` is the authoritative discriminator.

TradeCapture maps the generated `oneof` into the correct `IInstrumentReferenceDetails` record. Protobuf cannot directly use a C# interface because the wire contract is language-neutral data rather than .NET behaviour.

### Reference-data client behaviour

`GrpcReferenceDataClient`:

- validates the requested symbol;
- sends the correlation ID in gRPC metadata;
- converts `InstrumentId` from string to `Guid`;
- maps the generated asset-class enum into the shared platform enum;
- maps the protobuf `oneof` into FX, equity or bond detail records;
- validates and parses bond maturity dates;
- maps generated day-count convention into the shared platform enum;
- rejects invalid IDs, unsupported enum values and missing details.

---

## 6. Pricing and execution price

### Bid, ask and mid

```text
Bid = price at which the market will buy from us
Ask = price at which the market will sell to us
Mid = midpoint between bid and ask
```

Execution rule for the current cash asset classes:

```csharp
OrderSide.Buy  => quote.Ask
OrderSide.Sell => quote.Bid
```

A buyer pays the ask; a seller receives the bid.

The temporary PricingService constructs a deterministic quote from a mid and total spread:

```text
Bid = Mid - Spread / 2
Ask = Mid + Spread / 2
Spread = Ask - Bid
```

Current runtime examples:

```text
EURUSD
Mid 1.0850, spread 0.0002 -> Bid 1.0849, Ask 1.0851

AAPL
Mid 210.25, spread 0.50 -> Bid 210.00, Ask 210.50

GB00TEST1234
Mid 98.45, spread 0.10 -> Bid 98.40, Ask 98.50
```

In a real platform, bid and ask normally arrive from exchanges, banks, market makers, liquidity providers or aggregated feeds. Spreads vary with liquidity, volatility, time of day, order size, venue, broker markup and commission model.

`Mid` is useful for valuation and unrealised P&L, but is normally not the executable price.

The current execution-price rule remains valid for FX, equities and price-quoted bonds. Future fixed-income contracts may need quotation metadata for yield, spread, clean price, dirty price or RFQ results.

---

## 7. Notional calculations

### FX

The order quantity represents base-currency units. The result is in quote currency.

```text
100,000 EUR × 1.0851 USD/EUR = 108,510 USD
```

### Equity

The order quantity represents shares.

```text
100 shares × 210.50 USD = 21,050 USD
```

### Fixed income

For the current institutional-style model, bond `Quantity` represents nominal amount, not the count of physical bond units.

A bond price of `98.50` means 98.50% of nominal amount:

```text
1,000,000 GBP nominal × 98.50 / 100 = 985,000 GBP
```

The calculator therefore uses a percentage price quotation basis of `100`:

```text
quantity × execution price / 100
```

`ParValue` and price quotation basis are related but not identical concepts:

- par/face value can describe principal per bond unit;
- nominal amount describes the total principal represented by the position;
- price quotation basis describes how the market price is expressed.

Because the current bond quantity is already total nominal amount, multiplying by `ParValue` again would overstate the trade value.

This is currently clean-price trade value only. Future fixed-income work may add accrued interest, dirty price, coupon cash flows, settlement and yield analytics.

---

## 8. Live Strategy pattern in TradeCapture

The notional calculation code was initially modelled in ReferenceDataService.Domain while the multi-asset concepts were being explored. Once runtime ownership became clear, the calculator interface, three strategies, resolver and tests were moved together into TradeCaptureService.

ReferenceDataService answers:

> What is this instrument?

TradeCaptureService answers:

> How is this accepted trade priced, valued and captured?

Current structure:

```text
INotionalCalculator
    |-- FxNotionalCalculator
    |-- EquityNotionalCalculator
    |-- BondNotionalCalculator

NotionalCalculatorResolver
```

Each calculator declares the `AssetClass` it handles and accepts the full `InstrumentReferenceDefinition`, quantity and execution price. The full definition is retained because future calculations, especially fixed income, require asset-specific metadata.

Runtime use:

```csharp
var calculator = notionalCalculatorResolver.Resolve(
    instrument.AssetClass);

var notional = calculator.Calculate(
    instrumentReferenceDefinition,
    message.Quantity,
    executionPrice);
```

### Why this is Strategy

Clues:

1. one business action has several interchangeable algorithms;
2. every algorithm implements the same interface;
3. the caller does not contain one growing asset-class calculation switch;
4. one algorithm is selected at runtime;
5. the selected object performs the behaviour.

Memory aid:

> Same business action, different algorithm, selected at runtime.

For this platform:

> Same action: calculate trade notional. Different algorithms: FX, equity and fixed income. Selected at runtime from the instrument's asset class.

`NotionalCalculatorResolver` is a normal class rather than a record because it is a behaviour/service object containing dependencies and a lookup dictionary. Value equality is not a meaningful business operation for resolver instances.

The strategies and resolver are stateless and registered as singletons. DI supplies `IEnumerable<INotionalCalculator>` to the resolver, which builds:

```text
Fx          -> FxNotionalCalculator
Equity      -> EquityNotionalCalculator
FixedIncome -> BondNotionalCalculator
```

Duplicate strategies for the same asset class cause `ToDictionary` to fail during construction, exposing invalid configuration early.

---

## 9. Trade persistence and database design

### Trade fields added for multi-asset identity

```text
InstrumentId
AssetClass
NotionalCurrency
```

`Symbol` remains because it is useful for humans and operational queries.

Meaning:

```text
InstrumentId       stable identity received from ReferenceDataService
Symbol             human-readable business identifier
AssetClass         Fx, Equity or FixedIncome
Notional            calculated trade value
NotionalCurrency   currency in which Notional is expressed
```

`NotionalCurrency` uses the shared `CurrencyCode` value object in C# and an explicit EF value conversion:

```text
CurrencyCode -> database string
string       -> CurrencyCode
```

No custom `ToString()` override is required for EF conversion; EF uses `CurrencyCode.Value` explicitly.

### Indexes

```text
IX_Trades_OrderId       unique
IX_Trades_InstrumentId  non-unique
```

The unique `OrderId` index supports database-level idempotency: one accepted order can produce only one captured trade.

The non-unique `InstrumentId` index supports future instrument-level trade history, position, exposure and P&L queries.

On SQL Server, these are normally nonclustered because the primary key normally occupies the clustered index. SQLite does not use SQL Server's clustered/nonclustered terminology.

Dropping the `Trades` table automatically drops its rows, primary key, check constraints and indexes. A separate `DropIndex` is not required before `DropTable`.

### SQL Server check constraints

The clean initial TradeCapture migration contains constraints for:

```text
AssetClass       in Fx, Equity, FixedIncome
Side             in Buy, Sell
OrderType        in Market, Limit
Status           in Captured, Cancelled, Amended
InstrumentId     not Guid.Empty
NotionalCurrency exactly three uppercase A-Z characters
Quantity         greater than zero
Price            greater than zero
Notional         greater than zero
```

String enum conversions alone do not protect the database from direct SQL such as `Side = 'ABCDEFG'`; check constraints provide final database-level protection.

The SQL Server constraints use SQL Server-specific expressions such as:

```text
LEN(...)
COLLATE Latin1_General_100_BIN2
NOT LIKE '%[^A-Z]%'
```

### SQLite in-memory handler tests

`OrderAcceptedHandlerTests` deliberately keep the existing SQLite in-memory setup:

```csharp
await using var dbContext = CreateDbContext(connection);
await dbContext.Database.EnsureCreatedAsync();
```

Production configuration is not polluted with provider branches.

The test project contains:

```text
SqliteTradeDbContext
SqliteTradeCheckConstraintConfiguration
```

The test-only context applies the complete production configuration, removes only the SQL Server-dialect constraints, and replaces them with equivalent SQLite expressions such as:

```text
length([NotionalCurrency]) = 3
NOT GLOB '*[^A-Z]*'
```

This preserves the fast in-memory handler tests without duplicating the whole entity configuration or changing production code.

### Development migration-reset policy

At this early stage, if a service schema changes materially and its data is disposable:

```text
Update-Database 0
-> run existing Down migration(s)
-> remove migration files
-> correct the model and configuration
-> generate one clean Initial...Schema migration
-> inspect Up and Down
-> apply to the development database
```

`Remove-Migration` removes migration code; it does not by itself run `Down()` against an applied database.

Do not create fake defaults such as empty asset class, empty currency or `Guid.Empty` merely to preserve disposable development rows. Do not hardcode temporary reference-data symbols into a migration to guess historical asset classes.

Once data is valuable, this reset policy ends and changes must use forward, data-preserving migrations.

---

## 10. `TradeCaptured` multi-asset contract

`TradeCaptured` now carries:

```text
TradeId
OrderId
InstrumentId
ClientId
Symbol
AssetClass
Side
Quantity
Price
Notional
NotionalCurrency
Status
CapturedAt
CorrelationId
```

The event and domains share `TradingApp.SharedKernel.AssetClass`; no domain-to-contract enum mapping is needed inside TradeCapture.

The protobuf asset-class enum remains separate and is explicitly mapped at the gRPC boundary.

`Trade.NotionalCurrency` is deliberately persisted even though the reference details expose currency. The trade is a historical record of the currency used when it was captured; later reference-data changes must not rewrite historical trade meaning.

---

## 11. Runtime validation completed

The following services were run together:

```text
TradingGateway.Api
OrderService
RiskService.Grpc
ReferenceDataService.Grpc
PricingService.Grpc
TradeCaptureService
RabbitMQ
SQL Server
```

### EURUSD

Example buy:

```text
Quantity          100,000 EUR
Ask               1.0851
Notional          108,510 USD
InstrumentId      11111111-1111-1111-1111-111111111111
AssetClass        Fx
NotionalCurrency  USD
```

### AAPL

Swagger correlation ID:

```text
phase-2d-aapl-001
```

Example buy:

```text
Quantity          100 shares
Ask               210.50
Notional          21,050 USD
InstrumentId      22222222-2222-2222-2222-222222222222
AssetClass        Equity
NotionalCurrency  USD
```

### Fixed-income test instrument

Swagger correlation ID:

```text
phase-2d-bond-001
```

Example buy:

```text
Quantity          1,000,000 GBP nominal
Ask               98.50 per 100 nominal
Notional          985,000 GBP
InstrumentId      33333333-3333-3333-3333-333333333333
AssetClass        FixedIncome
NotionalCurrency  GBP
```

The runtime database rows confirmed that the resolver selected the expected strategy and persisted the expected instrument identity, asset class, execution price, notional and currency.

Correlation IDs were propagated across HTTP, NServiceBus and outgoing gRPC metadata.

---

## 12. Testing completed in Phase 2D

Important coverage includes:

### ReferenceData domain and gRPC

- FX, equity and bond detail validation;
- shared `CurrencyCode` validation and value equality;
- bond denomination currency;
- `InstrumentDefinition` composition;
- protobuf `oneof` mapping;
- gRPC service responses for each asset class;
- correlation metadata/log context.

### TradeCapture reference-data client

- common field mapping;
- FX/equity/bond detail mapping;
- notional-currency derivation from details;
- outgoing correlation metadata;
- invalid instrument ID rejection;
- invalid maturity date and unsupported enum paths.

### Notional strategies

- FX quantity × price;
- equity share quantity × price;
- fixed-income nominal × price / 100;
- resolver selection for Fx, Equity and FixedIncome;
- unsupported asset-class rejection;
- strategy validation of matching instrument/detail types.

### Handler flow

- existing FX buy/sell behaviour;
- equity buy selects ask and persists `21,050 USD`;
- fixed-income buy selects ask and persists `985,000 GBP`;
- complete multi-asset fields in `Trade` and `TradeCaptured`;
- duplicate order protection;
- reference-data and pricing correlation propagation;
- SQLite in-memory persistence with test-only check-constraint adaptation.

---

## 13. Shared Kernel decisions

`TradingApp.SharedKernel` now contains business concepts with intentionally identical semantics across services:

```text
AssetClass
CurrencyCode
DayCountConvention
Guard
```

### Why `CurrencyCode` is a `readonly record struct`

It is a small immutable value object. The compiler generates value equality, hash code, `==`, `!=`, `ToString()` and `with` support.

The custom constructor exists for business behaviour:

- reject null/blank values;
- trim;
- uppercase invariantly;
- require exactly three ASCII letters.

No custom equality or hash code is required.

### Why detail snapshots are records

The FX, equity and bond reference-detail objects are immutable snapshots. Two instances containing the same values can reasonably be considered equal.

### Why services are classes

Handlers, clients, calculators and resolvers provide behaviour and contain dependencies. Comparing two instances by value is not a useful business operation, so normal classes are appropriate.

### Shared Kernel caution

Only genuinely shared business concepts belong here. Service-specific entities and behaviour must remain inside the owning service. The Shared Kernel must not become a dumping ground that recreates a distributed monolith.

---

## 14. Modern C# constructs used

### `=>` expression body

```csharp
public CurrencyCode NotionalCurrency => QuoteCurrency;
```

Equivalent to:

```csharp
public CurrencyCode NotionalCurrency
{
    get
    {
        return QuoteCurrency;
    }
}
```

For multiple statements, use braces and an explicit `return`.

The same arrow token is also used by lambdas and switch expressions; its exact meaning depends on context.

### `nameof`

```csharp
nameof(instrumentDefinition)
```

produces the compile-time string `"instrumentDefinition"`. It is commonly supplied as an exception parameter name and follows refactoring better than a manually typed string.

It does not automatically update a database migration or a check constraint. Database schema changes still require a deliberate migration.

### `with`

```csharp
var request = CreateValidRequest() with
{
    Symbol = symbol
};
```

Creates a shallow copy and changes only the selected member. It is useful for records in focused tests.

### Nullable reference types and null-forgiving operator

`Metadata?` means the variable may be null.

`capturedHeaders!` tells the compiler to suppress the nullable warning at that point; it does not perform a runtime null check.

### Records

Records generate value-based equality and a generated `ToString()`. Override `ToString()` only when a different textual representation is deliberately required.

### `ValueType` and `object`

Struct inheritance is implicit in C# source:

```text
CurrencyCode -> System.ValueType -> System.Object
Int32        -> System.ValueType -> System.Object
```

A class without an explicit base class implicitly inherits `System.Object`.

### Type aliases

Aliases make protobuf/domain collisions explicit:

```csharp
using GrpcAssetClass = ReferenceDataService.Grpc.AssetClass;
using PlatformAssetClass = TradingApp.SharedKernel.AssetClass;
```

### Async assertion chain

```csharp
await act.Should()
    .ThrowAsync<InvalidOperationException>()
    .WithMessage("*invalid InstrumentId*");
```

`await` applies to the complete chained asynchronous assertion. `*` is FluentAssertions wildcard matching, not a new C# string feature and not a regular expression.

---

## 15. Patterns currently present

- Repository: repository abstractions and implementations.
- Strategy: live asset-specific notional algorithms plus runtime resolver.
- Mapper/Anti-Corruption Layer: protobuf types mapped into service-owned models.
- Dependency Injection: constructor-injected abstractions and multiple strategy implementations.
- Unit of Work and Repository in transactional services.
- Outbox with NServiceBus SQL persistence.
- Composition over inheritance: common instrument plus one details object.
- Value Object: `CurrencyCode`.
- Shared Kernel: narrowly shared business vocabulary.

---

## 16. Current project structure

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
  TradingApp.SharedKernel/

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

All projects target .NET 8. The solution-root `global.json` should pin the intended .NET 8 SDK with appropriate patch roll-forward.

---

## 17. Phase 2D status

### Completed

```text
OrderAccepted
    -> TradeCaptureService
    -> ReferenceDataService.GetInstrument(symbol)
    -> PricingService.GetPrice(symbol)
    -> select bid/ask execution price
    -> NotionalCalculatorResolver.Resolve(assetClass)
    -> selected calculator.Calculate(full instrument definition, quantity, price)
    -> persist Trade with InstrumentId, AssetClass, Notional and currency
    -> publish multi-asset TradeCaptured
```

Runtime validation is complete for:

```text
Fx
Equity
FixedIncome
```

Phase 2D is ready for a milestone commit after the updated architecture notes are copied into the repository and the complete build/test suite passes.

---

## 18. Next phase: Phase 2E — Multi-Asset Position and P&L

### Important finding

The position lifecycle mechanics are reusable across asset classes:

```text
open
add
reduce
close
flip
```

The realised P&L formula is not identical across all asset classes.

Examples:

```text
Equity:
quantity × (sell price - average buy price)

FX:
base quantity × price difference
result initially in quote currency

Fixed income:
nominal quantity × price difference / 100
```

Using the existing `quantity × price difference` formula for a bond would overstate realised P&L by a factor of 100.

Future fixed-income total P&L may also include accrued interest and coupon cash flows. Future account-level reporting may require currency conversion into an account/base reporting currency.

### Position database reset and redesign

Because the Position database is still early-stage development data, the current Position migration can be rolled back and regenerated cleanly after the model is finalised.

Before generating the new migration, review and add the appropriate multi-asset fields to both `Position` and `PositionMovement`.

Likely design topics:

```text
Position
  InstrumentId
  Symbol
  AssetClass
  PnlCurrency / valuation currency
  NetQuantity
  AveragePrice
  RealisedPnl
  UnrealisedPnl

PositionMovement
  TradeId
  OrderId
  InstrumentId
  Symbol
  AssetClass
  Side
  Quantity
  Price
  Notional
  NotionalCurrency
  previous/new position values
  realised P&L change and currency
```

The exact currency property names must be decided before the migration. Do not add ambiguous duplicate fields merely because they are available on the event.

The natural position identity should be reviewed. A stable candidate is:

```text
ClientId + InstrumentId
```

rather than relying only on `ClientId + Symbol`.

Add database constraints for enums, positive/valid values and non-empty IDs, using the same production-SQL/test-SQLite separation already established for TradeCapture.

### Position calculation design

Keep one position lifecycle engine, but delegate asset-specific realised P&L to a strategy selected by `AssetClass`.

Conceptual flow:

```text
TradeCaptured
    -> load/create position by ClientId + InstrumentId
    -> resolve realised-P&L strategy by AssetClass
    -> apply open/add/reduce/close/flip lifecycle
    -> calculate realised P&L with correct quotation basis
    -> persist Position and PositionMovement
    -> publish PositionUpdated
```

### Order database review before resetting

Do not automatically reset or extend OrderService merely because PositionService needs multi-asset fields.

First decide the OrderService responsibility:

**Option A — order stores submitted intent only**

```text
ClientId, Symbol, Side, OrderType, Quantity, Status
```

ReferenceData lookup remains in TradeCapture, so OrderService has no authoritative source for `InstrumentId` or `AssetClass`. In this design, adding empty or guessed multi-asset columns to Order is wrong.

**Option B — order owns validated instrument identity**

OrderService (or an earlier validation stage) must call ReferenceDataService before publishing `OrderAccepted`, then persist and publish `InstrumentId` and `AssetClass`.

This is a real architectural decision, not merely a migration edit. Review it before resetting the Order migration.

Current recommendation for the next session:

1. inspect `Order`, `OrderAccepted`, risk request and current processing order;
2. decide whether reference validation belongs before or after risk;
3. reset OrderService migration only if the model is deliberately changed;
4. definitely redesign and reset PositionService for multi-asset position/P&L.

---

## 19. Later roadmap

### Market and limit orders

```text
Market -> execute immediately using executable bid/ask
Limit  -> execute only when the price condition is met; otherwise remain Working
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

- multi-asset realised P&L;
- unrealised P&L;
- asset-specific valuation;
- FX conversion into account/reporting currency;
- position and P&L time series;
- exposure reports;
- dashboards and charts.

### FIX

- inbound new orders;
- mapping to internal `SubmitOrder`;
- outbound execution reports and rejects;
- preservation of external and internal identifiers.

### ReferenceData persistence

- replace the in-memory repository with EF Core/SQL;
- preserve stable instrument IDs;
- seed controlled development reference data;
- add instrument lifecycle/versioning and audit;
- avoid coupling transactional-service migrations to sample reference data.

---

## 20. Build, test and commit

Before the Phase 2D milestone commit:

```powershell
dotnet build
dotnet test
git status
```

Then:

```bash
git add .
git status
git commit -m "Complete Phase 2D multi-asset trade capture integration"
```

Suggested detailed commit body:

```text
- integrate ReferenceDataService with TradeCaptureService
- propagate correlation IDs over gRPC
- map full FX, equity and bond instrument definitions
- persist instrument identity, asset class and notional currency
- add SQL constraints, indexes and clean initial Trade schema
- add test-only SQLite check-constraint adaptation
- move notional strategies and resolver into TradeCapture
- use Strategy pattern in the live handler flow
- extend TradeCaptured with multi-asset fields
- consolidate shared business vocabulary into SharedKernel
- validate live FX, equity and fixed-income order flows
```

Suggested next-page title:

```text
Trading App Phase 2E – Multi-Asset Position and P&L
```
