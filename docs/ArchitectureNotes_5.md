# ETrading Platform — Architecture Notes

**Project:** ETrading Platform  
**Stack:** .NET 8, C#, NServiceBus, RabbitMQ, EF Core, SQL Server, gRPC, Serilog, NetMQ / ZeroMQ, System.Threading.Channels  
**Current state:** Multi-asset trading flow plus live ZeroMQ market-data ingestion, gRPC pricing, trade capture, positions and realised P&L working end-to-end  
**Last updated:** 2026-08-22

---

# 1. Purpose of This File

This is the single cumulative architecture and learning document for the ETrading Platform.

It is intentionally not phase-specific.

The purpose is to keep in one place:

- current service architecture
- completed design decisions
- important implementation details
- naming decisions
- database decisions
- testing strategy
- C# learning notes
- trading-domain explanations
- known limitations
- future roadmap
- interview-oriented concepts such as ZeroMQ, sockets and messaging

This file should be extended over time instead of creating separate architecture notes per phase.

---

# 2. High-Level Platform Architecture

Current service flow:

```text
MarketDataSimulator
  ↓ ZeroMQ PUB
PricingService.Grpc
  ├── ZeroMQ SUB
  ├── MarketQuoteCache
  └── gRPC GetPrice
          ↑
          │
Client
  ↓
TradingGateway.Api
  ↓
SubmitOrder
  ↓
OrderService
  ↓
RiskService.Grpc
  ↓
OrderAccepted
  ↓
TradeCaptureService
  ├── ReferenceDataService.Grpc
  ├── PricingService.Grpc GetPrice
  ├── ExecutionPrice selection
  └── Asset-specific trade value / notional strategy
  ↓
Trade persisted
  ↓
TradeCaptured
  ↓
PositionService
  ├── Position lifecycle
  ├── Asset-specific realised P&L strategy
  ├── Position updated
  ├── PositionMovement inserted
  └── ProcessedTrade inserted
  ↓
PositionUpdated
```

The system currently supports:

```text
FX
Equity
Fixed Income / Bonds
```

The key Phase 3A change is that PricingService no longer manufactures executable prices from hard-coded mid/spread dictionaries. It now receives transient market-data ticks over ZeroMQ, keeps the latest Bid/Ask per symbol in memory, and serves the current quote through the existing gRPC `GetPrice` boundary.

---

# 3. Main Services and Responsibilities

## 3.1 TradingGateway.Api

Responsibilities:

- HTTP/API entry point
- accepts client order payload
- manages/request propagates correlation ID
- creates/sends the order command into the messaging workflow
- Swagger API entry point

Important principle:

> Gateway is an API boundary, not the business owner of order execution or position accounting.

---

## 3.2 OrderService

Responsibilities:

- owns the order lifecycle
- stores submitted order state
- calls RiskService
- publishes order acceptance or rejection events
- protects order processing/idempotency through the service architecture

Important current limitation:

- OrderService receives the client symbol before ReferenceData lookup.
- Therefore authoritative `InstrumentId` and `AssetClass` are not currently added to OrderService merely for convenience.
- Do not wire ReferenceData into OrderService unless the order-domain design genuinely requires it.

---

## 3.3 RiskService.Grpc

Responsibilities:

- validates whether an order is allowed
- current rules include:
  - maximum quantity
  - allowed symbols
  - known client
- returns structured rejection reason / reason code
- preserves correlation metadata

Current allowed sample symbols include:

```text
EURUSD
USDJPY
AAPL
GB00TEST1234
```

RiskService answers:

> Are we willing to permit this order?

It does not calculate:

- price
- notional
- trade value
- position
- P&L

---

## 3.4 ReferenceDataService.Grpc

Responsibilities:

> What instrument is this?

ReferenceData is authoritative for relatively stable instrument identity and definitions.

Common data:

```text
InstrumentId
Symbol
AssetClass
IsTradable
```

Asset-specific data:

### FX

```text
BaseCurrency
QuoteCurrency
PipSize
```

### Equity

```text
Exchange
TradingCurrency
```

### Bond

```text
ISIN
Issuer
DenominationCurrency
CouponRate
MaturityDate
ParValue
DayCountConvention
```

ReferenceData does not own the current executable market price.

Current temporary in-memory stable IDs:

```text
EURUSD
11111111-1111-1111-1111-111111111111

AAPL
22222222-2222-2222-2222-222222222222

GB00TEST1234
33333333-3333-3333-3333-333333333333
```

These IDs are temporary sample data only.

When ReferenceData gains a real database, the in-memory seed implementation should disappear.

Do not treat these temporary hard-coded IDs as production truth.

---

## 3.5 PricingService.Grpc

Responsibilities:

> Maintain the current executable market quote and answer: what Bid/Ask is available now?

Current inbound market-data flow:

```text
MarketDataSimulator
    ↓ ZeroMQ PUB
ZeroMqPriceSubscriber
    ↓
PriceTickSubscriberWorker
    ↓
MarketQuoteCache
    ↓
PricingGrpcService.GetPrice
```

Current quote model:

```text
Bid
Ask
Mid = (Bid + Ask) / 2
```

`Bid` and `Ask` are authoritative. `Mid` is derived. The old hard-coded `MidPrices` and `Spreads` dictionaries have been removed from the runtime pricing path.

`PriceTick` uses `decimal` internally:

```csharp
public sealed record PriceTick(
    string Symbol,
    decimal Bid,
    decimal Ask,
    DateTimeOffset Timestamp);
```

The current protobuf response uses `double`, so the conversion is explicit at the gRPC boundary:

```csharp
var bid = (double)priceTick.Bid;
var ask = (double)priceTick.Ask;
var mid = (bid + ask) / 2;
```

Financial calculations should remain `decimal` internally. The transport conversion should stay at the boundary rather than leaking `double` through domain calculations.

If no quote exists in `MarketQuoteCache`, `GetPrice` currently returns gRPC `StatusCode.Unavailable`. PricingService by itself cannot distinguish an unknown instrument from a known instrument for which no market-data tick has arrived; ReferenceData remains the authoritative source of instrument existence.

Important principle:

> ReferenceData tells us what the instrument is. Pricing tells us what it can currently trade at.

---

## 3.6 TradeCaptureService

Responsibilities:

- consumes `OrderAccepted`
- calls ReferenceDataService
- calls PricingService
- selects executable Bid/Ask according to side
- resolves asset-specific notional/trade-value calculation
- persists Trade
- publishes `TradeCaptured`

Core flow:

```text
OrderAccepted
    ↓
ReferenceData lookup
    ↓
InstrumentReferenceDefinition
    ↓
Pricing lookup
    ↓
GetExecutionPrice()
    ↓
NotionalCalculatorResolver
    ↓
Trade persisted
    ↓
TradeCaptured
```

---

## 3.7 PositionService

Responsibilities:

- consumes `TradeCaptured`
- identifies current position by client + authoritative instrument identity
- creates a position if none exists
- applies trade to current position
- handles:
  - open
  - add
  - reduce
  - close
  - flip
- calculates realised P&L
- records PositionMovement audit trail
- records processed trade
- publishes `PositionUpdated`

---

# 4. Technical Shared Assemblies

## 4.1 TradingApp.SharedKernel

Shared business vocabulary/value objects live here.

Current examples:

```text
Guard
CurrencyCode
AssetClass
DayCountConvention
```

Important design principle:

> SharedKernel should contain intentionally shared domain vocabulary, not service-specific behaviour.

Do not put a service's internal domain entity in SharedKernel merely because another service wants to reuse it.

---

## 4.2 TradingApp.Shared

Used for technical cross-cutting concerns such as:

- correlation support
- infrastructure/shared technical helpers

It is not the domain SharedKernel.

---

# 5. Service Boundary Rule

Do not project-reference another service's internal domain assembly.

Example of what not to do:

```text
TradeCaptureService
    → project reference
ReferenceDataService.Domain
```

Instead use the external service contract:

```text
TradeCaptureService
    → gRPC contract
ReferenceDataService.Grpc
```

Then map the transport DTO/protobuf message into TradeCapture's own internal model.

Reason:

- keeps services independently evolvable
- avoids hidden coupling
- keeps transport boundaries explicit
- prevents one service's internal implementation from becoming another service's dependency

---

# 6. Protobuf Enums vs SharedKernel Enums

Generated protobuf types remain transport types.

For example:

```text
protobuf AssetClass
```

is not treated as the same thing as:

```text
TradingApp.SharedKernel.AssetClass
```

Explicit mapping occurs at the gRPC boundary.

This is intentional.

Transport representation and domain vocabulary should not be accidentally coupled.

---

# 7. Protobuf Field Numbers

Example:

```proto
bool isTradable = 4;

oneof details {
  FxInstrumentDetails fxDetails = 5;
  EquityInstrumentDetails equityDetails = 6;
  BondInstrumentDetails bondDetails = 7;
}
```

The numbers are protobuf wire-field identifiers.

They are part of the serialized contract.

Important rules:

- once a field number has been used, do not casually reuse it for a different meaning
- changing property names is often safer than changing field numbers
- field numbers allow backward/forward-compatible serialization when contracts evolve carefully

---

# 8. ReferenceData TradeCapture Client

TradeCapture contains:

```text
IReferenceDataClient
GrpcReferenceDataClient
```

The gRPC client:

- propagates correlation metadata
- validates/parses instrument identity
- maps protobuf details into TradeCapture internal reference models

Important models:

```text
InstrumentReferenceData
InstrumentReferenceDefinition
IInstrumentReferenceDetails
FxInstrumentReferenceDetails
EquityInstrumentReferenceDetails
BondInstrumentReferenceDetails
```

---

# 9. InstrumentReferenceDefinition

Contains:

```text
InstrumentReferenceData Instrument
IInstrumentReferenceDetails Details
```

It validates that both refer to the same `InstrumentId`.

Do not expose duplicate convenience access paths unnecessarily.

For example, a forwarding property:

```text
InstrumentReferenceDefinition.NotionalCurrency
```

was deliberately removed.

Preferred access:

```csharp
instrumentReferenceDefinition.Details.NotionalCurrency
```

Reason:

> One authoritative path is easier to understand than two properties that represent the same thing.

---

# 10. Immutable Instrument Details

Reference detail snapshots use records:

```text
FxInstrumentReferenceDetails
EquityInstrumentReferenceDetails
BondInstrumentReferenceDetails
```

These are immutable descriptive data, so `sealed record` is appropriate.

General heuristic:

```text
EF entity              → class
service/behaviour      → class
immutable snapshot     → sealed record
small value object     → readonly record struct
```

---

# 11. CurrencyCode

`CurrencyCode` is a:

```csharp
readonly record struct
```

Responsibilities:

- represent currency code as a value type
- validate
- normalize
- make invalid values harder to pass around

Example:

```csharp
new CurrencyCode("usd")
```

can normalize internally to:

```text
USD
```

Do not add a custom `ToString()` merely to return `.Value` unless there is a genuine need.

A record struct already has generated value semantics.

For EF mapping, explicitly use:

```csharp
currencyCode.Value
```

rather than relying on `ToString()`.

---

# 12. Why readonly record struct?

Useful for a small value object like `CurrencyCode` because:

- value semantics
- immutable
- small
- no identity separate from its value
- equality behaves naturally

Example:

```csharp
new CurrencyCode("USD")
==
new CurrencyCode("USD")
```

conceptually means the same currency value.

---

# 13. C# `=>`

`=>` appears in several C# contexts.

## Expression-bodied member

```csharp
public AssetClass AssetClass => AssetClass.Fx;
```

Equivalent idea:

```csharp
public AssetClass AssetClass
{
    get { return AssetClass.Fx; }
}
```

## Lambda

```csharp
x => x.ClientId == clientId
```

Means:

> Given `x`, return whether this condition is true.

## Switch expression

Can also appear in:

```csharp
value switch
{
    ...
}
```

Memory rule:

> `=>` means “produces / returns this result” in the surrounding C# construct.

---

# 14. `nameof`

Example:

```csharp
throw new ArgumentException(
    "Trade quantity cannot be zero.",
    nameof(tradeSignedQuantity));
```

`nameof(tradeSignedQuantity)` produces:

```text
"tradeSignedQuantity"
```

at compile time.

Useful for:

- exceptions
- property names
- refactoring safety

However:

> Do not use `nameof(...)` inside database check-constraint SQL strings.

Reason:

Renaming the enum member later would not automatically create a required database migration.

For SQL constraints use explicit persisted strings such as:

```text
'Fx'
'Equity'
'FixedIncome'
```

---

# 15. Null-Forgiving Operator `!`

Example:

```csharp
something!
```

Means:

> Compiler, I know this value is not null here.

It affects nullable-reference warnings.

It does not add a runtime null check.

Use sparingly.

Prefer designs that make null-state obvious rather than suppressing warnings everywhere.

---

# 16. Record `with` Expression

Example used in tests:

```csharp
CreateValidRequest() with
{
    Symbol = symbol
}
```

This creates a copy of a record with selected properties changed.

It is a shallow copy.

Useful in tests because it avoids rebuilding an entire immutable request merely to change one property.

---

# 17. `ToUpperInvariant`

Use for culture-independent normalization.

For domain identifiers/codes:

```text
usd → USD
eur → EUR
```

`Invariant` prevents the current machine/user culture from changing behaviour.

Appropriate for machine/domain codes rather than human-language text.

---

# 18. Correlation ID

Correlation IDs are important throughout the platform.

HTTP header:

```text
X-Correlation-Id
```

Flow:

```text
Gateway
    ↓
commands/events
    ↓
gRPC metadata
    ↓
service logs
```

Important goals:

- one business request traceable across services
- Serilog enrichment
- gRPC metadata propagation
- NServiceBus message propagation
- runtime diagnosis

ReferenceData, Pricing, Risk and Trade flows must preserve correlation.

---

# 19. Serilog

Services use Serilog with correlation enrichment.

ReferenceDataService was explicitly wired to its service-specific Serilog extension.

Correlation values should appear in logs so a full request path can be followed.

---

# 20. Bid / Ask / Mid / Spread

Definitions:

```text
Bid
= price market/provider is willing to buy at

Ask
= price market/provider is willing to sell at

Spread
= Ask - Bid

Mid
= approximately midpoint between Bid and Ask
```

Example:

```text
Mid    210.25
Spread   0.50

Bid = 210.00
Ask = 210.50
```

---

# 21. Execution Price Rule

Current cash-style execution model:

```text
Buy  → Ask
Sell → Bid
```

Reason:

If client buys, they cross to the seller's Ask.

If client sells, they cross to the buyer's Bid.

The method was renamed from:

```csharp
Calculate(...)
```

to:

```csharp
GetExecutionPrice(...)
```

because the operation is really selecting the executable side of the quote rather than performing a substantial calculation.

Preferred readability:

```csharp
var executionPrice =
    executionPriceCalculator.GetExecutionPrice(
        message.Side,
        quote);
```

---

# 22. Future Pricing Complexity

Current execution-price behaviour is intentionally simple.

Future bond or institutional markets may involve:

- clean vs dirty price
- yield
- spread
- RFQ
- dealer quotes
- multiple LPs
- venue/source
- sizes/depth
- last look
- best execution

Do not prematurely make the current quote model complicated before those use cases exist.

---

# 23. Notional / Trade Value Strategy

TradeCapture has:

```text
INotionalCalculator
FxNotionalCalculator
EquityNotionalCalculator
BondNotionalCalculator
NotionalCalculatorResolver
```

Interface:

```csharp
public interface INotionalCalculator
{
    AssetClass AssetClass { get; }

    decimal Calculate(
        InstrumentReferenceDefinition instrumentDefinition,
        decimal quantity,
        decimal price);
}
```

The full instrument definition remains part of the input intentionally.

Do not simplify the interface to quantity/price merely because the current formulas are simple.

Future fixed-income calculation may require more instrument detail.

---

# 24. Strategy Pattern — Notional

Clues:

- same operation
- multiple algorithms
- runtime selection
- common interface
- resolver chooses implementation

Operation:

```text
calculate trade value/notional
```

Selector:

```text
AssetClass
```

Strategies:

```text
FxNotionalCalculator
EquityNotionalCalculator
BondNotionalCalculator
```

Memory rule:

> Same job, different rule → Strategy.

---

# 25. FX Trade Value / Notional

Current formula:

```text
quantity × execution price
```

Example:

```text
Buy 100,000 EURUSD @ 1.0851

100,000 × 1.0851
= 108,510 USD
```

---

# 26. Equity Trade Value / Notional

Current formula:

```text
shares × execution price
```

Example:

```text
100 AAPL × 210.50
= 21,050 USD
```

---

# 27. Bond Trade Value / Notional

Current bond quantity convention:

```text
Quantity = nominal amount
```

Example:

```text
Quantity = 1,000,000 GBP nominal
Price    = 98.50
```

Bond price is percentage quoted.

Formula:

```text
nominal × price / 100
```

So:

```text
1,000,000 × 98.50 / 100
= 985,000 GBP
```

Constant:

```text
PercentagePriceBasis = 100
```

Do not confuse this `100` with bond `ParValue`.

It represents percentage quotation basis.

---

# 28. Terminology Note — Notional vs Trade Value

There is a naming issue worth revisiting later.

For bonds:

```text
Quantity = nominal amount
```

is already the nominal/notional.

The calculation:

```text
nominal × market price / 100
```

is more naturally:

```text
trade value
market value
consideration
```

Similarly equity:

```text
100 shares × price
```

is naturally trade value.

Current names remain:

```text
INotionalCalculator
Notional
NotionalCurrency
```

because renaming now would ripple through:

- contracts
- entities
- migrations
- tests
- handlers

Possible future cleanup:

```text
ITradeValueCalculator
TradeValue
TradeValueCurrency
```

Do this deliberately, not casually.

---

# 29. Why Notional and P&L Calculators Stay Separate

They have similar formula shapes.

For equity:

```text
Trade value:
quantity × execution price

Realised P&L:
closed quantity × price difference
```

For bond:

```text
Trade value:
nominal × price / 100

Realised P&L:
closed nominal × price difference / 100
```

Do not merge them just because arithmetic looks similar.

They answer different business questions:

```text
Trade value:
What monetary size/value did this trade represent?

Realised P&L:
How much money did the closed part make or lose?
```

Memory rule:

> Same arithmetic does not automatically mean same responsibility.

---

# 30. Trade Entity

Trade includes:

```text
OrderId
InstrumentId
ClientId
Symbol
AssetClass
Side
OrderType
Quantity
Price
Notional
NotionalCurrency
Status
CorrelationId
timestamps
```

Trade retains `Symbol` even though `InstrumentId` is authoritative.

Reason:

- human-readable
- logs
- reports
- operational diagnostics

---

# 31. Trade Database Constraints

Production SQL Server constraints include:

```text
CK_Trades_AssetClass
CK_Trades_Side
CK_Trades_OrderType
CK_Trades_Status
CK_Trades_InstrumentId_NotEmpty
CK_Trades_NotionalCurrency
```

Examples:

```sql
[AssetClass] COLLATE Latin1_General_100_BIN2
IN ('Fx','Equity','FixedIncome')
```

```sql
[Side] ...
IN ('Buy','Sell')
```

```sql
[OrderType] ...
IN ('Market','Limit')
```

```sql
[Status] ...
IN ('Captured','Cancelled','Amended')
```

InstrumentId cannot be all-zero Guid.

NotionalCurrency must be 3 uppercase ASCII characters.

Quantity, Price and Notional must be positive.

---

# 32. EF CurrencyCode Mapping in Trade

Pattern:

```csharp
.HasConversion(
    currencyCode => currencyCode.Value,
    databaseValue => new CurrencyCode(databaseValue))
.HasMaxLength(3)
.IsUnicode(false)
.IsFixedLength()
.IsRequired();
```

Do not rely on `ToString()` for persistence.

---

# 33. Production DB vs SQLite Test DB

Production is SQL Server.

Tests use SQLite in-memory where useful.

Do not branch inside production `DbContext` based on provider merely to satisfy tests.

Bad direction:

```text
if SQLite
    do one schema
else SQL Server
    do another
```

inside production infrastructure.

Preferred approach:

```text
Production DbContext
    → clean SQL Server model

Test-only SqliteDbContext subclass
    → base production model
    → remove incompatible SQL Server constraints
    → add SQLite equivalents
```

This pattern is used in:

```text
TradeCaptureService.Tests
PositionService.Tests
```

---

# 34. SQLite Test Config Naming

TradeCapture pattern:

```text
SqliteTradeCheckConstraintConfiguration
SqliteTradeDbContext
```

Position pattern:

```text
PositionService.Tests/
  Infrastructure/
    Persistence/
      SqlitePositionDbContext
      SqlitePositionCheckConstraintConfiguration
      SqlitePositionMovementCheckConstraintConfiguration
```

Keep the structure consistent across test projects.

---

# 35. Why `base.OnModelCreating(modelBuilder)`?

When overriding:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // additional changes
}
```

C# does not automatically execute the parent implementation.

`base.OnModelCreating(...)` means:

> First build the normal inherited production model, then customise it.

Without it, a test DbContext could lose:

- indexes
- relationships
- conversions
- precision
- required properties
- production mappings

Memory rule:

> override + base first = keep normal behaviour, then customise.

---

# 36. SQLite SQL Differences

Common differences:

```text
SQL Server                  SQLite

LEN(...)                    length(...)
NOT LIKE '%[^A-Z]%'         NOT GLOB '*[^A-Z]*'
SQL Server collation        generally not used the same way
```

SQLite test constraints should preserve the business intent even if syntax differs.

---

# 37. Early Development Migration Policy

At this stage, dev/test data is disposable.

When schema changes are significant, avoid generating fake defaults such as:

```text
Guid.Empty
""
```

for new required business fields.

That creates false business data.

Preferred early-development reset:

```text
Update-Database 0
Remove-Migration
Remove-Migration
...
Add-Migration clean initial schema
Update-Database
```

This was used for:

```text
TradeCaptureService
PositionService
```

Later, when real/valuable data exists:

> Use forward, data-preserving production migrations.

Do not use destructive reset techniques once data matters.

---

# 38. Package Manager Console Switches

Example:

```powershell
Add-Migration InitialPositionSchema -Project PositionService -StartupProject PositionService -Context PositionDbContext -OutputDir Infrastructure\Persistence\Migrations
```

Meaning:

```text
-Project
= where DbContext / migration files live

-StartupProject
= which executable EF runs for startup/configuration/DI
```

They may be the same project, but they represent different concepts.

Give Package Manager Console commands on one line for reliable copy/paste.

---

# 39. NServiceBus Handler Transaction Rule

Inside an NServiceBus handler:

> Do not manually call `transactionalSession.Commit()`.

The handler already executes inside NServiceBus message processing.

Gateway is different because it originates a transaction outside message processing and may explicitly open/commit a transactional session.

Important note:

True coordination between EF persistence and outbox/message publication remains an architectural concern.

Adding `Commit()` inside the handler does not magically solve transactional consistency.

---

# 40. TradeCaptured Event

Extended multi-asset fields:

```text
TradeId
OrderId
ClientId
InstrumentId
Symbol
AssetClass
NotionalCurrency
Side
Quantity
Price
CorrelationId
CapturedAt
...
```

`AssetClass` uses SharedKernel vocabulary.

`NotionalCurrency` remains a primitive string in the integration event.

Reason:

```text
Integration boundary
    → serialization-friendly primitive

Position domain
    → CurrencyCode value object
```

---

# 41. Trade vs Position

A Trade is one executed transaction.

A Position is the running accumulated state.

Example:

```text
Trade 1: Buy 100 AAPL @ 200
Trade 2: Buy  50 AAPL @ 220
Trade 3: Sell 40 AAPL @ 230
```

Trade history:

```text
three separate records
```

Position:

```text
NetQuantity
AveragePrice
RealisedPnl
...
```

Memory rule:

```text
Trade
= what happened

Position
= what is held now
```

---

# 42. Position Identity

Previously:

```text
ClientId + Symbol
```

Now:

```text
ClientId + InstrumentId
```

Unique index:

```csharp
entity.HasIndex(e => new
{
    e.ClientId,
    e.InstrumentId
})
.IsUnique();
```

Meaning:

> One current position per client per authoritative instrument.

`Symbol` remains for readability.

---

# 43. Position Model

Important fields:

```text
Id
ClientId
InstrumentId
Symbol
AssetClass

NetQuantity
AveragePrice

PnlCurrency
RealisedPnl
UnrealisedPnl

CorrelationId
CreatedAt
UpdatedAt
```

---

# 44. Position P&L Currency

`PnlCurrency` is required.

Examples:

```text
EURUSD → USD
AAPL   → USD
UK bond → GBP
```

A naked P&L number is incomplete.

Example:

```text
RealisedPnl = 1000
```

must also say whether it is:

```text
USD
GBP
EUR
...
```

---

# 45. PositionMovement

`Position` stores current state.

`PositionMovement` stores the audit trail of how each trade changed the position.

Important fields:

```text
PositionId
TradeId
OrderId
ClientId
InstrumentId
Symbol
AssetClass

Side
Quantity
SignedQuantity
Price

PreviousNetQuantity
PreviousAveragePrice

NewNetQuantity
NewAveragePrice

PreviousRealisedPnl
RealisedPnlChange
NewRealisedPnl

PnlCurrency
CorrelationId
CreatedAt
```

---

# 46. RealisedPnl Field Rename

Old movement field:

```text
RealisedPnl
```

was ambiguous.

New:

```text
PreviousRealisedPnl
RealisedPnlChange
NewRealisedPnl
```

Example:

```text
PreviousRealisedPnl = 500
RealisedPnlChange   = 100
NewRealisedPnl      = 600
```

Interpretation:

```text
PreviousRealisedPnl
= running total before this trade

RealisedPnlChange
= P&L created by this trade

NewRealisedPnl
= running total after this trade
```

---

# 47. Why Sum RealisedPnlChange?

Example:

```text
Movement 1 change = 100, total = 100
Movement 2 change =  50, total = 150
Movement 3 change =  20, total = 170
```

Correct:

```text
100 + 50 + 20 = 170
```

Wrong:

```text
100 + 150 + 170 = 420
```

Therefore:

```csharp
Movements.Sum(x => x.RealisedPnlChange)
```

can reconstruct cumulative realised P&L.

Do not sum `NewRealisedPnl`.

---

# 48. Signed Quantity

Trade side is converted into arithmetic sign:

```text
Buy  → positive
Sell → negative
```

Examples:

```text
Buy 100  → +100
Sell 40  → -40
```

Then:

```text
current position + signed trade
= new position
```

Example:

```text
+100 long
-40 sell
-------
+60 remaining
```

Memory rule:

> Signed quantity turns Buy/Sell into arithmetic.

---

# 49. NetQuantity

Interpretation:

```text
positive
= long

negative
= short

zero
= flat / closed
```

Examples:

```text
+100 AAPL
= long 100 shares

-100 AAPL
= short 100 shares

0
= no current open exposure
```

---

# 50. AveragePrice

Represents the weighted average entry price for the current open position.

Example:

```text
Buy 100 @ 200
Buy  50 @ 220
```

```text
AveragePrice
= (100×200 + 50×220) / 150
= 206.6667
```

When reducing a position, remaining units keep the existing average price.

When flipping, the new opposite position starts at the new trade price.

When fully closing, average price becomes zero.

---

# 51. Position Lifecycle — OPEN

Start:

```text
NetQuantity = 0
```

Trade:

```text
Buy 100 @ 200
```

Signed:

```text
+100
```

Result:

```text
NetQuantity  = +100
AveragePrice = 200
RealisedPnl  = 0
```

Nothing has been closed, so no realised P&L.

---

# 52. Position Lifecycle — ADD

Same direction as existing position.

Example long:

```text
Existing +100
Trade    +50
Result   +150
```

Example short:

```text
Existing -100
Trade    -50
Result   -150
```

Average price becomes weighted average.

Realised P&L remains zero.

---

# 53. Position Lifecycle — REDUCE

Opposite trade, but does not cross zero.

Example:

```text
Existing +100
Trade     -40
Result    +60
```

Closed quantity:

```text
40
```

Remaining position stays long.

Remaining average price remains the existing average price.

Closed portion may generate realised P&L.

---

# 54. Position Lifecycle — CLOSE

Opposite trade exactly offsets existing position.

Example:

```text
Existing +100
Trade    -100
Result      0
```

Result:

```text
NetQuantity  = 0
AveragePrice = 0
```

Closed amount generates realised P&L.

---

# 55. Position Lifecycle — FLIP

Trade crosses through zero.

Example:

```text
Existing +100
Trade    -150
Result    -50
```

Meaning:

```text
first 100 of the sell
    closes old long

remaining 50
    opens new short
```

Only the closed 100 generates realised P&L.

New short starts at the incoming trade price.

---

# 56. Math.Sign in PositionCalculator

Used to compare direction.

Conceptually:

```text
+100 → +1
-100 → -1
```

Same direction:

```csharp
Math.Sign(existingNetQuantity) ==
Math.Sign(tradeSignedQuantity)
```

means:

```text
long + buy
or
short + sell
```

---

# 57. Math.Abs in PositionCalculator

Used when only magnitude matters.

Example:

```text
Existing +100 → abs = 100
Trade     -40 → abs = 40
```

Closed quantity:

```csharp
Math.Min(existingAbs, tradeAbs)
```

handles:

- reduce
- close
- flip

correctly.

---

# 58. Long vs Short P&L Price Difference

Long:

```text
exit price - average entry price
```

Example:

```text
Buy @ 200
Sell @ 210

+10
```

Short:

```text
average entry sell price - buy-back price
```

Example:

```text
Sell short @ 200
Buy back   @ 180

+20
```

Current logic:

```csharp
var priceDifference = existingNetQuantity > 0
    ? tradePrice - existingAveragePrice
    : existingAveragePrice - tradePrice;
```

---

# 59. PositionCalculator Responsibility

`PositionCalculator` owns the common lifecycle:

```text
open
add
reduce
close
flip
```

It also determines:

```text
closed quantity
new net quantity
new average price
long/short price difference
```

It does not own asset-specific money conventions.

---

# 60. Realised P&L Strategy

Interface:

```csharp
public interface IRealisedPnlCalculator
{
    AssetClass AssetClass { get; }

    decimal Calculate(
        decimal closedQuantity,
        decimal priceDifference);
}
```

Strategies:

```text
FxRealisedPnlCalculator
EquityRealisedPnlCalculator
BondRealisedPnlCalculator
```

Resolver:

```text
RealisedPnlCalculatorResolver
```

---

# 61. Realised P&L Strategy Architecture

```text
PositionCalculator
    determines:
        what quantity closed
        whether price difference is profit/loss

        ↓

RealisedPnlCalculatorResolver
        ↓

Fx / Equity / Bond strategy
        ↓

monetary realised P&L
```

This avoids duplicating position lifecycle logic across every asset class.

---

# 62. FX Realised P&L

Current formula:

```text
closed quantity × price difference
```

Example:

```text
Average = 1.0800
Sell    = 1.0900
Closed  = 40

40 × 0.0100
= 0.4000 USD
```

Loss:

```text
40 × -0.0100
= -0.4000 USD
```

---

# 63. Equity Realised P&L

Current formula:

```text
closed shares × price difference
```

Example:

```text
Average = 200
Sell    = 210
Closed  = 40

40 × 10
= 400 USD
```

---

# 64. Bond Realised P&L

Current formula:

```text
closed nominal × price difference / 100
```

Example:

```text
Nominal = 1,000,000 GBP
Average = 98.50
Sell    = 99.50
Difference = 1.00

1,000,000 × 1.00 / 100
= 10,000 GBP
```

Without `/100` the result would be catastrophically wrong.

---

# 65. RealisedPnlCalculatorResolver

Maps:

```text
Fx
    → FxRealisedPnlCalculator

Equity
    → EquityRealisedPnlCalculator

FixedIncome
    → BondRealisedPnlCalculator
```

Uses:

```csharp
IReadOnlyDictionary<AssetClass, IRealisedPnlCalculator>
```

created from:

```csharp
IEnumerable<IRealisedPnlCalculator>
```

The resolver is a class because it is behaviour/service logic, not immutable value data.

---

# 66. DI for P&L Strategies

Example registrations:

```csharp
services.AddSingleton<IRealisedPnlCalculator, FxRealisedPnlCalculator>();
services.AddSingleton<IRealisedPnlCalculator, EquityRealisedPnlCalculator>();
services.AddSingleton<IRealisedPnlCalculator, BondRealisedPnlCalculator>();

services.AddSingleton<RealisedPnlCalculatorResolver>();
services.AddSingleton<PositionCalculator>();
```

.NET DI automatically collects all registered implementations into:

```csharp
IEnumerable<IRealisedPnlCalculator>
```

---

# 67. Array vs List in Tests

Test setup uses:

```csharp
new IRealisedPnlCalculator[]
{
    new FxRealisedPnlCalculator(),
    new EquityRealisedPnlCalculator(),
    new BondRealisedPnlCalculator()
}
```

Why array?

- fixed set
- no Add/Remove needed
- communicates intent
- resolver only requires `IEnumerable<T>`

`List<T>` would also work.

This is primarily a readability/intent choice, not a performance decision.

---

# 68. PositionUpdated

Now includes:

```text
PositionId
ClientId
InstrumentId
Symbol
AssetClass
NetQuantity
AveragePrice
RealisedPnl
UnrealisedPnl
PnlCurrency
CorrelationId
```

This prevents downstream consumers receiving ambiguous data.

Example bad event:

```text
AAPL
RealisedPnl = 500
```

Missing:

```text
which instrument?
which asset class?
500 in which currency?
```

Current event carries all necessary identity.

---

# 69. EF Change Tracking

Why does this update the database?

```csharp
position.NetQuantity = calculation.NewNetQuantity;
position.AveragePrice = calculation.NewAveragePrice;
position.RealisedPnl += calculation.RealisedPnl;
```

without calling:

```csharp
Update(position)
```

Because the entity was loaded by the same EF `DbContext`.

Normal EF query:

```csharp
context.Positions.SingleOrDefaultAsync(...)
```

returns a tracked entity unless `AsNoTracking()` is used.

EF remembers original values.

Example:

```text
Original

NetQuantity  = 100000
AveragePrice = 98.50
RealisedPnl  = 0
```

After property assignments:

```text
Current

NetQuantity  = -50000
AveragePrice = 98.40
RealisedPnl  = -100
```

At:

```csharp
SaveChangesAsync()
```

EF detects changes and generates an SQL `UPDATE`.

---

# 70. New Entity vs Existing Entity

Memory rule:

```text
Existing entity loaded by DbContext
    → tracked
    → change properties
    → SaveChanges
    → UPDATE

New object
    → Add / AddAsync
    → SaveChanges
    → INSERT
```

This is why `PositionMovement` uses `AddAsync`.

It is a brand-new object.

---

# 71. Why No UpsertAsync?

Current business flow explicitly separates:

```text
position does not exist
    → create
    → AddAsync

position exists
    → tracked
    → mutate
    → SaveChanges
```

An `UpsertAsync()` abstraction is not required and would hide meaningful lifecycle behaviour.

---

# 72. AsNoTracking

If the repository used:

```csharp
.AsNoTracking()
```

then EF would not automatically detect modifications.

You would need to attach/update explicitly.

Therefore:

> Queries for entities that will be modified should normally stay tracked.

---

# 73. ChangeTracker Learning Test

`EfLearningTests` includes a change-tracking example showing:

```text
load entity
    ↓
EntityState.Unchanged

change properties
    ↓
DetectChanges()
    ↓
EntityState.Modified

SaveChangesAsync()
    ↓
UPDATE
    ↓
EntityState.Unchanged
```

Useful inspection:

```csharp
entry.Property(x => x.NetQuantity).OriginalValue
entry.Property(x => x.NetQuantity).CurrentValue
entry.Property(x => x.NetQuantity).IsModified
```

In normal application code explicit `DetectChanges()` is generally unnecessary because SaveChanges triggers it.

---

# 74. Reusing dbContext Variable Names in Separate Blocks

This is valid:

```csharp
await using (var dbContext = ...)
{
}

await using (var dbContext = ...)
{
}
```

because each variable exists only in its own scope.

The first variable is disposed and goes out of scope before the second declaration.

This naming style is preferred here because:

```text
dbContext
```

immediately communicates what the variable is.

There is no need to invent names like:

```text
setupContext
trackingContext
verificationContext
```

when separate scopes already make lifecycle clear.

---

# 75. await using

Example:

```csharp
await using var connection =
    new SqliteConnection("DataSource=:memory:");
```

This does not mean object creation is asynchronous.

It means:

> Dispose this object asynchronously at the end of the scope.

Conceptually:

```text
using
    → Dispose()

await using
    → DisposeAsync()
```

Compare:

```csharp
await connection.OpenAsync();
```

which means:

> Perform and await an asynchronous operation now.

Memory rule:

```text
await SomeOperationAsync()
= async operation now

await using
= async cleanup later
```

---

# 76. NServiceBus Test PublishedMessages Chain

Example:

```csharp
var published = messageContext.PublishedMessages.Single().Message
    .Should()
    .BeOfType<PositionUpdated>()
    .Subject;
```

Breakdown:

```text
PublishedMessages
    → collection

.Single()
    → exactly one wrapper

.Message
    → actual published object

.Should()
    → FluentAssertions

.BeOfType<PositionUpdated>()
    → assert exact type

.Subject
    → return typed PositionUpdated
```

---

# 77. Single vs First vs SingleOrDefault

```text
First()
= at least one; take first even if extras exist

Single()
= exactly one must exist

SingleOrDefault()
= zero or one
```

For a handler expected to publish exactly one event:

```csharp
Single()
```

communicates the invariant correctly.

---

# 78. IntelliSense and Extension Methods

IntelliSense often mixes:

- instance methods
- properties
- extension methods

This makes fluent APIs harder to read.

Do not try to memorise long chains.

Instead break them into temporary variables and inspect return types.

Example:

```csharp
var wrapper =
    messageContext.PublishedMessages.Single();

var message =
    wrapper.Message;

var assertion =
    message.Should()
        .BeOfType<PositionUpdated>();

var published =
    assertion.Subject;
```

Memory rule:

> Ask “what type/value do I have now?” then “what value do I need next?”

---

# 79. Position Database Constraints

Production `Positions` constraints include:

```text
CK_Positions_AssetClass
CK_Positions_InstrumentId_NotEmpty
CK_Positions_PnlCurrency
```

Do not constrain:

```text
NetQuantity > 0
```

because:

```text
positive = long
negative = short
zero     = flat
```

Do not constrain:

```text
RealisedPnl >= 0
```

because losses are valid.

Do not force:

```text
AveragePrice > 0
```

because closed position may validly have:

```text
NetQuantity  = 0
AveragePrice = 0
```

---

# 80. PositionMovement Constraints

Constraints include:

```text
CK_PositionMovements_AssetClass
CK_PositionMovements_InstrumentId_NotEmpty
CK_PositionMovements_PnlCurrency
```

Do not reject negative:

```text
SignedQuantity
NewNetQuantity
RealisedPnlChange
NewRealisedPnl
```

because shorts and losses are legitimate.

---

# 81. Runtime Verification — AAPL

Live end-to-end equity example:

```text
Symbol = AAPL
Buy 100
```

ReferenceData:

```text
InstrumentId = 22222222-2222-2222-2222-222222222222
AssetClass   = Equity
Currency     = USD
```

Pricing:

```text
Bid = 210.00
Ask = 210.50
```

Buy uses:

```text
Ask = 210.50
```

Trade value:

```text
100 × 210.50
= 21,050 USD
```

Trade persisted successfully.

---

# 82. Runtime Verification — Bond Trade Capture

Example:

```text
Symbol   = GB00TEST1234
Quantity = 1,000,000 nominal
Buy
```

ReferenceData:

```text
InstrumentId = 33333333-3333-3333-3333-333333333333
AssetClass   = FixedIncome
Currency     = GBP
```

Pricing:

```text
Bid = 98.40
Ask = 98.50
```

Buy:

```text
ExecutionPrice = 98.50
```

Trade value:

```text
1,000,000 × 98.50 / 100
= 985,000 GBP
```

---

# 83. Runtime Verification — Bond OPEN Position

Live order:

```text
Buy 100,000 nominal GB00TEST1234
```

Execution:

```text
Ask = 98.50
```

Position:

```text
InstrumentId = 33333333-3333-3333-3333-333333333333
AssetClass   = FixedIncome
NetQuantity  = +100,000
AveragePrice = 98.50
RealisedPnl  = 0
PnlCurrency  = GBP
```

This proves OPEN end-to-end.

---

# 84. Runtime Verification — Bond FLIP

Second live order:

```text
Sell 150,000 nominal
```

Execution:

```text
Bid = 98.40
```

Arithmetic:

```text
Existing +100,000
Trade    -150,000
        ---------
Result    -50,000
```

Closed quantity:

```text
100,000
```

Price difference:

```text
98.40 - 98.50
= -0.10
```

Realised P&L:

```text
100,000 × -0.10 / 100
= -100 GBP
```

Final position:

```text
NetQuantity  = -50,000
AveragePrice = 98.40
RealisedPnl  = -100
PnlCurrency  = GBP
```

Movement:

```text
PreviousNetQuantity  = +100,000
PreviousAveragePrice = 98.50

NewNetQuantity       = -50,000
NewAveragePrice      = 98.40

PreviousRealisedPnl  = 0
RealisedPnlChange    = -100
NewRealisedPnl       = -100
```

This proves multi-asset position accounting end-to-end.

---

# 85. Current Test Coverage

Important tests include:

- Risk rules
- correlation propagation
- ReferenceData client mapping
- malformed/missing data behaviour
- notional calculators
- notional resolver
- TradeCapture handler FX
- TradeCapture handler equity
- TradeCapture handler bond
- EF SQLite constraints
- Position EF relationships
- Position FK behaviour
- Position change tracking learning tests
- PositionCalculator:
  - open long
  - open short
  - add long
  - add short
  - reduce long profit/loss
  - reduce short profit/loss
  - close long
  - close short
  - flip long→short
  - flip short→long
- FX realised P&L calculator
- Equity realised P&L calculator
- Bond realised P&L calculator
- realised P&L resolver
- TradeCaptured handler
- PositionMovement before/change/after fields
- PositionUpdated fields
- bond close
- bond flip
- live runtime open/flip verification

---

# 86. Current Known Limitations

Not yet implemented:

- live market data
- real limit-order working lifecycle
- partial fills
- cancel/amend
- unrealised P&L / mark-to-market
- live position valuation
- fees/commissions
- accrued bond interest
- clean vs dirty bond price
- coupon cashflows
- yield/spread valuation
- multiple liquidity providers
- best execution
- market depth
- ReferenceData persistence
- SignalR UI
- FIX connectivity
- execution reports
- portfolio-level risk aggregation
- FX conversion of portfolio P&L

These are future enhancements, not defects in the current phase.

---

# 87. Recommended Roadmap

Current roadmap:

```text
Phase 3A — COMPLETE
Live Market Data with ZeroMQ

Phase 3B — NEXT
Working Limit Orders

Phase 3C
Unrealised P&L / Mark-to-Market

Phase 3D
SignalR Live UI

Phase 3E
Market Watch / Position / P&L Charts

Later
Cancel/amend
Partial fills
Execution reports
FIX
Multiple LPs
Best execution
Portfolio risk
Richer fixed income
```

---

# 88. Why ZeroMQ Before Limit Orders

Current market order behaviour only needs:

```text
give me current executable price now
```

A real limit order needs to wait while prices move.

Example:

```text
Buy AAPL limit 200

Ask 205
    → wait

Ask 203
    → wait

Ask 200
    → trigger
```

Therefore:

```text
live prices first
working limit orders second
```

is the cleaner architecture.

---

# 89. Implemented Live Market Data Architecture

PricingService gRPC was deliberately retained. Phase 3A changed where PricingService obtains prices, not the service boundary used by TradeCapture.

Implemented architecture:

```text
MarketDataSimulator
        ↓ ZeroMQ PUB
PricingService.Grpc
        ↓ ZeroMQ SUB
PriceTickSubscriberWorker
        ↓
MarketQuoteCache
        ↓
existing PricingService gRPC GetPrice
        ↓
TradeCaptureService
```

This preserves the PricingService boundary. TradeCapture does not care whether prices came from:

- the current simulator
- a future exchange/vendor feed
- one or more liquidity providers
- a future aggregation layer

The market-data transport is therefore hidden behind PricingService.

---

# 90. Why Keep gRPC?

ZeroMQ and gRPC solve different problems.

ZeroMQ:

```text
push stream of continuously changing prices
```

gRPC:

```text
request/reply:
Give me current quote for this symbol now
```

Recommended combination:

```text
ZeroMQ
    feeds PricingService cache

gRPC
    exposes current quote to TradeCapture
```

---

# 91. Why Keep RabbitMQ / NServiceBus?

Market prices are transient.

Business events are durable.

Missing one price tick may be acceptable if a newer quote immediately arrives.

Missing:

```text
TradeCaptured
```

would be unacceptable.

Therefore:

```text
ZeroMQ
    → fast transient market-data stream

NServiceBus / RabbitMQ
    → durable business workflow
```

Do not replace order/trade business messaging with ZeroMQ merely for speed.

---

# 92. What Is a Socket?

A socket is a communication endpoint used by a process.

With TCP, communication commonly involves:

```text
IP address
port
protocol
```

Example:

```text
tcp://127.0.0.1:5555
```

Conceptually:

```text
Process A
    ↓
socket
    ↓
TCP/IP
    ↓
socket
    ↓
Process B
```

---

# 93. Raw TCP

TCP gives a reliable ordered byte stream.

Raw TCP does not inherently know:

```text
message 1
message 2
message 3
```

It sees bytes.

Application concerns may include:

- framing
- partial reads
- partial writes
- reconnect
- buffering
- protocol format
- multiple clients
- concurrency
- backpressure

---

# 94. What ZeroMQ Adds

ZeroMQ is a messaging library built around socket-like abstractions.

It can use TCP underneath while providing:

- message boundaries
- internal queues
- reconnect behaviour
- messaging patterns
- multipart messages
- topic subscription
- easier application-to-application messaging

Interview statement:

> ZeroMQ is not a replacement network protocol for TCP. It is a higher-level messaging library that can use TCP underneath while providing message-oriented socket abstractions.

---

# 95. ZeroMQ Is Not a Traditional Broker

RabbitMQ:

```text
Producer
    ↓
Broker
    ↓
Consumer
```

ZeroMQ often:

```text
Application
    ⇄
ZeroMQ socket transport
    ⇄
Application
```

No mandatory central broker for common patterns.

ZeroMQ still has internal queues/buffering.

The "Zero" does not mean messages are literally never queued.

---

# 96. ZeroMQ PUB/SUB

Most relevant first pattern for market data.

```text
Publisher
    ↓
Subscriber(s)
```

For platform:

```text
MarketDataSimulator
    PUB
     ↓
PricingService
    SUB
```

Publisher continuously sends price ticks.

Subscriber listens.

---

# 97. Why PUB/SUB Fits Market Data

Market data is naturally one-to-many.

Publisher does not need to know every consumer.

Possible subscribers later:

```text
PricingService
Limit Order Engine
Risk
UI
Historical Recorder
Analytics
```

This decoupling is useful for market-data dissemination.

---

# 98. Bind vs Connect

Common ZeroMQ concept.

Example publisher:

```text
Bind("tcp://*:5555")
```

Meaning:

> Own/listen on local endpoint.

Subscriber:

```text
Connect("tcp://localhost:5555")
```

Meaning:

> Connect to exposed endpoint.

Useful interview memory:

```text
Bind
= expose/listen

Connect
= attach to endpoint
```

---

# 99. ZeroMQ Transports

Common:

```text
tcp://
ipc://
inproc://
```

## TCP

Across processes/machines.

Best initial choice for the platform.

## IPC

Inter-process communication on same host.

## inproc

Communication inside same process.

Initial implementation should prefer:

```text
tcp://localhost:<port>
```

for clarity.

---

# 100. Message Framing

Raw TCP is byte-stream based.

ZeroMQ is message-oriented.

It preserves message boundaries.

This reduces the need to design low-level framing rules manually.

---

# 101. Multipart Messages

ZeroMQ can send multiple frames as one logical message.

Possible market-data structure:

```text
Frame 1
topic / symbol

Frame 2
payload
```

Example:

```text
AAPL

{"bid":210.00,"ask":210.50}
```

Useful later for topic subscriptions.

Do not overcomplicate the first version.

---

# 102. Serialization Is Separate from Transport

ZeroMQ transports messages/bytes.

It does not define the business schema.

Possible serialization:

```text
JSON
protobuf
MessagePack
custom binary
```

Recommended first learning implementation:

```text
JSON
```

Then later compare binary/protobuf performance.

Interview point:

> Transport and serialization are separate concerns.

---

# 103. Slow Subscriber

If publisher is faster than subscriber:

```text
publisher
tick tick tick tick tick
        ↓
subscriber slower
```

queues can grow.

Important concerns:

- high-water mark
- dropping stale prices
- freshness
- backpressure
- memory growth

For market data, latest price may matter more than processing every stale tick.

---

# 104. High-Water Mark

ZeroMQ has queue limits / high-water-mark concepts.

This helps bound queued messages.

Questions for later design:

- should stale price messages be dropped?
- what queue size is safe?
- what happens if PricingService stalls?
- should freshest price win?

This is relevant interview knowledge for low-latency systems.

---

# 105. Slow Joiner Problem

PUB/SUB subscribers can miss messages around startup while subscriptions/connections become active.

Known concept:

```text
slow joiner
```

Do not publish one critical startup message and assume every subscriber saw it.

A continuously streaming simulator naturally reduces the impact.

PricingService should handle:

```text
no quote received yet
```

explicitly.

---

# 106. ZeroMQ Thread Ownership

Useful rule:

> Treat a ZeroMQ socket as owned by one thread unless documentation explicitly says otherwise.

Do not casually share one socket across unrelated workers.

PricingService subscriber should likely be a dedicated hosted/background service with clear socket ownership.

---

# 107. Blocking vs Non-Blocking Receive

Receiving market data may block waiting for data.

Service design must consider:

- cancellation
- graceful shutdown
- polling
- timeouts
- CPU spinning
- background thread/task ownership

Do not block the gRPC request thread waiting for a market-data tick.

---

# 108. ZeroMQ vs RabbitMQ

## RabbitMQ strengths

- durable broker
- queues
- acknowledgements
- routing
- persistence
- reliable workflow
- operational management

Good for:

```text
OrderAccepted
TradeCaptured
PositionUpdated
```

## ZeroMQ strengths

- lightweight
- low latency
- direct app-to-app
- socket patterns
- no mandatory broker
- high-rate transient streams

Good for:

```text
market ticks
quotes
fast streaming data
```

---

# 109. ZeroMQ vs Raw TCP

Raw TCP offers lower-level control but requires more application infrastructure.

Raw TCP concerns include:

- framing
- reconnect
- buffering
- connection lifecycle
- protocol design
- multi-client handling
- send/receive loops

ZeroMQ provides higher-level messaging behaviour on top of socket transports.

---

# 110. ZeroMQ vs gRPC

gRPC:

```text
RPC / request-response / streaming contract
```

ZeroMQ:

```text
socket-oriented messaging patterns
```

In platform:

```text
ZeroMQ
    pushes changing prices into PricingService

gRPC
    lets TradeCapture ask PricingService for latest quote
```

Complementary, not competing.

---

# 111. MarketDataSimulator

`MarketDataSimulator` is now implemented as a .NET 8 console application using NetMQ.

It publishes independently moving ticks for:

```text
EURUSD
AAPL
GB00TEST1234
```

Current sample starting parameters:

```text
EURUSD
initial Bid 1.0849
spread      0.0002
step        0.0001
delay       100–400 ms

AAPL
initial Bid 210.00
spread      0.50
step        0.25
delay       250–800 ms

GB00TEST1234
initial Bid 98.40
spread      0.10
step        0.05
delay       500–1500 ms
```

Each instrument simulator is an independent producer. Producers write `PriceTick` values into a bounded `Channel<PriceTick>`. One publisher worker owns the ZeroMQ `PublisherSocket` and publishes ticks from the channel.

Purpose:

- teach ZeroMQ and socket concepts
- exercise changing prices
- support working limit-order development
- support future mark-to-market P&L
- avoid depending on a real market-data vendor during development

---

# 112. PriceTick Contract

Shared assembly:

```text
TradingApp.MarketData.Contracts
```

Current contract:

```csharp
public sealed record PriceTick(
    string Symbol,
    decimal Bid,
    decimal Ask,
    DateTimeOffset Timestamp);
```

Why only Bid and Ask?

```text
Mid    = (Bid + Ask) / 2
Spread = Ask - Bid
```

Therefore Mid and Spread are derived values and do not need to be authoritative fields in the tick.

Possible future fields:

```text
InstrumentId
Venue
Source
SequenceNumber
BidSize
AskSize
Currency
Status
```

Do not add them until the business requirement appears.

---

# 113. Sequence Numbers

Useful in real feeds to detect gaps/out-of-order messages.

Example:

```text
100
101
103
```

Missing:

```text
102
```

The current simulator does not yet include a sequence number. This remains an important future improvement when stale/out-of-order/gap detection becomes a requirement.

---

# 114. Market Data Timestamps

Current `PriceTick.Timestamp` uses `DateTimeOffset`.

Potential future timestamps:

```text
exchange/event timestamp
publisher timestamp
consumer receive timestamp
```

These allow latency measurement such as:

```text
receive latency = consumerReceiveTime - publisherTime
```

The current simulator uses one minimal timestamp and does not yet measure transport latency.

---

# 115. MarketQuoteCache

The chosen name is `MarketQuoteCache`, replacing the earlier working name `MarketQuoteCache`.

Business meaning:

> PricingService's current in-memory view of the latest Bid/Ask received for each symbol.

Current flow:

```text
ZeroMqPriceSubscriber
        ↓
PriceTickSubscriberWorker
        ↓ Update
MarketQuoteCache
        ↓ TryGet
PricingGrpcService.GetPrice
```

The cache uses:

```csharp
ConcurrentDictionary<string, PriceTick>
```

The writer uses `AddOrUpdate`, so a new tick replaces the previous tick for the same symbol.

`ConcurrentDictionary` is appropriate because the ZeroMQ subscriber worker updates the cache while gRPC request threads can read it concurrently.

`TryGet` uses nullable annotations to describe its contract:

```csharp
public bool TryGet(
    string symbol,
    [NotNullWhen(true)] out PriceTick? tick)
```

`PriceTick?` means null is an allowed state when no value exists. `[NotNullWhen(true)]` tells the compiler that when `TryGet` returns `true`, the out value is guaranteed to be non-null.

`MarketQuoteCache` is registered as a singleton so the subscriber worker and gRPC service use the same in-memory state.

---

# 116. Limit Orders

Current model already contains:

```text
OrderType.Market
OrderType.Limit
```

but Limit is not yet genuinely implemented.

A real limit order needs:

```text
LimitPrice
```

Example:

```json
{
  "clientId": "client-001",
  "symbol": "AAPL",
  "side": "Buy",
  "quantity": 100,
  "orderType": "Limit",
  "limitPrice": 200.00
}
```

---

# 117. Buy Limit Rule

Execute when:

```text
Ask <= LimitPrice
```

Example:

```text
Buy limit = 200

Ask 205 → wait
Ask 203 → wait
Ask 200 → execute
Ask 199 → execute
```

---

# 118. Sell Limit Rule

Execute when:

```text
Bid >= LimitPrice
```

Example:

```text
Sell limit = 220

Bid 215 → wait
Bid 218 → wait
Bid 220 → execute
Bid 221 → execute
```

---

# 119. Working Order Lifecycle

Market order:

```text
Accepted
    ↓
ready immediately
    ↓
execute
```

Limit order:

```text
Accepted
    ↓
Working
    ↓
market ticks
    ↓
condition met?
    ├── No → keep Working
    └── Yes → trigger execution
```

This means:

> `OrderAccepted` should not necessarily mean immediate execution for every order type.

---

# 120. Future Execution Event Boundary

Current:

```text
OrderAccepted
    ↓
TradeCapture
```

works for Market orders.

Limit orders may require a distinct concept:

```text
OrderAccepted
    ↓
Execution decision
    ↓
OrderReadyForExecution / OrderTriggered
    ↓
TradeCapture
```

Exact naming should be decided after inspecting the current OrderService execution flow.

Do not simply add:

```csharp
if (OrderType == Limit)
```

inside TradeCapture and call the design complete.

---

# 121. Unrealised P&L / Mark-to-Market

Once live prices exist, calculate current open-position P&L.

Example equity:

```text
Long 100 AAPL
Average = 200
Current mark = 210

Unrealised P&L
= 100 × (210 - 200)
= 1000 USD
```

Bond valuation must again respect percentage-price basis.

Future design question:

- PositionService consumes market data directly?
- separate ValuationService?
- valuation events?

Do not decide prematurely.

---

# 122. SignalR UI

Once prices/positions change continuously, a UI should receive updates rather than constantly poll.

SignalR can push:

- price updates
- order status
- trades
- positions
- P&L
- risk

Potential UI:

```text
Market Watch
Order Blotter
Trade Blotter
Positions
P&L
Risk
Charts
```

---

# 123. FIX — Later

Future external execution connectivity may use FIX.

Possible boundary:

```text
Internal Order
    ↓
Execution / Routing
    ↓
FIX Adapter
    ↓
Broker / Venue
    ↓
ExecutionReport
    ↓
internal execution event
```

FIX should be kept as a transport/integration adapter.

Do not embed FIX-specific fields/logic directly into core domain behaviour unless necessary.

---

# 124. Multiple Liquidity Providers — Later

Possible future:

```text
LP1 quote
LP2 quote
LP3 quote
    ↓
aggregation
    ↓
best bid / best ask
    ↓
execution routing
```

Concepts:

- venue
- spread
- depth
- RFQ
- last look
- best execution
- smart order routing

---

# 125. Fixed-Income Enrichment — Later

Current bond model is simplified.

Future capabilities may include:

- clean price
- dirty price
- accrued interest
- coupon schedule
- settlement
- day-count calculation
- yield
- spread
- duration
- convexity
- RFQ

Current `/100` basis is correct for the simplified percentage-price model but is not full bond valuation.

---

# 126. Phase 3A Completion Checklist

Completed:

```text
✓ Create MarketDataSimulator
✓ Add TradingApp.MarketData.Contracts
✓ Define PriceTick
✓ Install NetMQ
✓ Learn PUB/SUB, Bind/Connect and multipart frames
✓ Add bounded Channel producer/consumer pipeline
✓ Add ZeroMqPricePublisher
✓ Add independent instrument simulators
✓ Add ZeroMqPriceSubscriber in PricingService
✓ Add PriceTickSubscriberWorker
✓ Add BackgroundService lifecycle integration
✓ Add MarketQuoteCache
✓ Make PricingGrpcService.GetPrice read live cache
✓ Remove runtime hard-coded Mid/Spread lookup
✓ Add cache/subscriber/worker/gRPC tests
✓ Runtime verify EURUSD, AAPL and bond live pricing
✓ Runtime verify Buy→Ask and Sell→Bid
✓ Runtime verify realised P&L from live execution prices
```

Immediate next phase:

```text
Phase 3B — Working Limit Orders
```

The live stream is now available to drive order-trigger decisions.

---

# 127. ZeroMQ Interview Checklist

Be able to explain:

1. What is a socket?
2. What does TCP provide?
3. Why does raw TCP need framing?
4. What does ZeroMQ add?
5. Is ZeroMQ a broker?
6. What is PUB/SUB?
7. Why does PUB/SUB fit market data?
8. What is bind vs connect?
9. What are tcp/ipc/inproc transports?
10. What is multipart messaging?
11. What is serialization?
12. Why is serialization separate from transport?
13. What happens when a subscriber is slow?
14. What is high-water mark?
15. What is the PUB/SUB slow-joiner issue?
16. Can disconnected subscribers miss ticks?
17. Why may that be acceptable for market data?
18. Why is it not acceptable for TradeCaptured?
19. Why keep RabbitMQ?
20. Why keep gRPC?
21. What are REQ/REP and PUSH/PULL?
22. Why should sockets have clear thread ownership?
23. How do you stop receive loops cleanly?
24. What do sequence numbers detect?
25. What timestamps matter?
26. What is backpressure?
27. Why can newest price matter more than old ticks?

---

# 128. Short ZeroMQ Interview Answer

A concise description of the planned platform design:

> The platform uses NServiceBus with RabbitMQ for durable business workflow such as order acceptance and trade capture. Live market data is a different workload, so ZeroMQ PUB/SUB is used for transient high-rate price ticks. A MarketDataSimulator publishes ticks over a ZeroMQ socket, PricingService subscribes in a background component and updates MarketQuoteCache, while TradeCapture continues to query the existing PricingService gRPC contract. This keeps the rest of the platform independent of the market-data transport and provides practical experience with bind/connect, sockets, framing, subscriptions, buffering, slow consumers and high-water marks.

---

# 129. Architecture Principles to Keep

## One service owns each responsibility

Avoid letting multiple services own the same business state.

## Stable service boundaries

Prefer mapping at boundaries over project-referencing another service's domain internals.

## Do not overengineer future use cases

Add abstractions when there is a genuine behavioural difference.

## Do not under-model real business differences

Bond P&L `/100` is a real domain difference and deserved a strategy.

## Same formula does not mean same responsibility

Notional/trade-value calculation and realised P&L remain separate.

## Prefer authoritative identity

Use `InstrumentId` for system identity.

Keep `Symbol` for human readability.

## Keep currencies explicit

Never treat naked monetary numbers as fully specified.

## Keep production model clean

Test-provider differences belong in test infrastructure.

## Avoid fake migration backfills

Guid.Empty and empty strings are not legitimate business values merely because EF wants defaults.

## Preserve auditability

Trade history + PositionMovement should explain current Position state.

---

# 130. C# Learning Checklist for This Project

Concepts encountered so far:

```text
class
record
sealed record
readonly record struct
value object
with expression
lambda
expression-bodied member
switch expression
nameof
null-forgiving !
ToUpperInvariant
IEnumerable<T>
IReadOnlyDictionary<TKey,TValue>
array vs List<T>
constructor injection
DI multiple-interface registration
Strategy pattern
resolver pattern
EF ValueConverter
EF change tracking
EntityState
OriginalValue / CurrentValue
IsModified
await
await using
IAsyncDisposable
extension methods
FluentAssertions chains
NServiceBus test context
gRPC metadata
protobuf oneof
protobuf field numbers
```

When adding unfamiliar C# constructs, add a short note here rather than treating syntax as something to memorise blindly.

---

# 131. Git Workflow

Currently single-developer workflow on `main`.

A feature branch is optional.

Recommended before commit:

```bash
git status
dotnet test
git diff --stat
git diff
```

Then:

```bash
git add .
git commit -m "Complete multi-asset positions and realised PnL"
git push origin main
```

As project/team size grows, feature branches and PR review may become worthwhile.

---

# 132. Current Completion State

The following is working end-to-end:

```text
MarketDataSimulator
    ↓ ZeroMQ PUB/SUB
PricingService MarketQuoteCache
    ↓ gRPC GetPrice
Client order
    ↓
Gateway
    ↓
OrderService
    ↓
Risk
    ↓
OrderAccepted
    ↓
ReferenceData
    ↓
Pricing
    ↓
Buy→Ask / Sell→Bid
    ↓
asset-specific trade value
    ↓
Trade
    ↓
TradeCaptured
    ↓
Position lookup by InstrumentId
    ↓
signed quantity
    ↓
open/add/reduce/close/flip
    ↓
asset-specific realised P&L
    ↓
Position
    ↓
PositionMovement
    ↓
PositionUpdated
```

Validated asset classes:

```text
FX
Equity
Fixed Income
```

Phase 3A runtime verification includes live EURUSD Buy/Sell, live AAPL Buy and live bond Buy. The EURUSD round-trip also verified realised P&L using changing live execution prices.

---

# 133. Next Major Milestone

Next major milestone:

```text
Phase 3B — Working Limit Orders
```

Now that prices move continuously, a limit order can genuinely rest and wait for a market condition.

Target concept:

```text
Buy Limit
trigger when Ask <= LimitPrice

Sell Limit
trigger when Bid >= LimitPrice
```

The next design work should decide:

- where working orders are persisted
- which service owns the working-order lifecycle
- how live price changes are delivered to the limit-order evaluator
- how Market vs Limit execution paths differ
- order statuses while resting, triggered, filled or cancelled
- idempotency and duplicate-trigger protection
- tests for non-trigger, trigger and restart behaviour

After Phase 3B:

```text
Phase 3C Unrealised P&L
Phase 3D SignalR UI
Phase 3E Charts
Later FIX / external execution
```

---

# 134. Final Memory Summary

If only a few things are remembered, remember these:

```text
Order
= what client wants

Trade
= what actually executed

Position
= what client holds now
```

```text
ReferenceData
= what instrument is this?

Pricing
= what can it trade at now?

Risk
= are we allowed to accept it?

TradeCapture
= what trade actually happened?

PositionService
= what do we hold now and what P&L is realised?
```

```text
Buy  = positive signed quantity
Sell = negative signed quantity
```

```text
OPEN   0 → position
ADD    bigger same direction
REDUCE smaller same direction
CLOSE  position → 0
FLIP   long ↔ short
```

```text
Strategy pattern
= same job, different rule
```

```text
Tracked EF entity
= change properties + SaveChanges → UPDATE

New EF entity
= Add/AddAsync + SaveChanges → INSERT
```

```text
RabbitMQ/NServiceBus
= durable business events

gRPC
= service request/reply

ZeroMQ
= implemented fast transient market-data stream
```

# 135. Phase 3A — Live Market Data Completed

Phase 3A changed the platform from static configured prices to a moving market-data feed while preserving the existing service boundaries.

Before:

```text
PricingService
    ↓
hard-coded Mid + Spread
    ↓
Bid / Ask
```

After:

```text
MarketDataSimulator
    ↓ ZeroMQ PUB
PricingService ZeroMQ SUB
    ↓
MarketQuoteCache
    ↓
GetPrice
    ↓
TradeCapture
```

This was deliberately done without replacing gRPC. ZeroMQ is the inbound streaming transport. gRPC remains the synchronous service contract used by TradeCapture to ask for the current executable quote.

---

# 136. Why ZeroMQ Fits Market Data

Market data has different reliability requirements from trade lifecycle events.

For market data:

```text
Tick 1 = 1.0849 / 1.0851
Tick 2 = 1.0850 / 1.0852
Tick 3 = 1.0851 / 1.0853
```

If Tick 1 is missed but Tick 3 arrives immediately afterwards, the system normally cares much more about the latest state than replaying every old tick.

For business events such as `TradeCaptured`, losing the event is not acceptable because it changes positions, P&L and audit state.

Therefore:

```text
ZeroMQ PUB/SUB
= transient current-state market-data flow

RabbitMQ / NServiceBus
= durable business workflow
```

This is a workload-based architecture decision rather than choosing one messaging technology for everything.

---

# 137. ZeroMQ Wire Protocol

The current protocol is one multipart ZeroMQ message containing exactly two frames:

```text
Frame 1
Topic / Symbol
EURUSD

Frame 2
JSON payload
{"Symbol":"EURUSD", ...}
```

Publisher code follows the logical-message chain:

```csharp
publisherSocket
    .SendMoreFrame(tick.Symbol)
    .SendFrame(payload);
```

`SendMoreFrame` means the logical message is not finished yet. `SendFrame` sends the final frame.

The subscriber receives the whole multipart message and validates:

```text
frames.Count == 2
```

and:

```text
topic == tick.Symbol
```

A mismatch such as:

```text
Topic: EURUSD
Payload.Symbol: AAPL
```

is treated as malformed market data and rejected.

---

# 138. Bind, Connect and Endpoint Choice

Publisher:

```text
tcp://*:5555
```

means:

> Bind/listen on port 5555 on available local interfaces.

PricingService subscriber:

```text
tcp://localhost:5555
```

means:

> Connect to the publisher on the current machine.

Tests use `BindRandomPort("tcp://127.0.0.1")` so parallel/local test execution does not depend on port 5555 being free.

---

# 139. ZeroMQ Queues and Why Receive Can Happen After Send

The application does not need to be executing `Receive` at the exact microsecond that the publisher sends.

Once the subscription is established, the message can move through internal/network buffers:

```text
publisher.Send
    ↓
ZeroMQ outbound queue
    ↓
TCP
    ↓
subscriber inbound queue
    ↓
application later calls TryReceive
```

This differs from sending before a subscriber is ready. PUB/SUB is not durable, so a message sent before the subscription is established can be lost permanently.

That startup property is the ZeroMQ slow-joiner problem.

---

# 140. Bounded Channel Between Simulators and Publisher

Each `InstrumentPriceSimulator` is a producer. One `PriceTickPublisherWorker` is the consumer and sole owner of the ZeroMQ publisher socket.

Pipeline:

```text
EURUSD simulator ─┐
AAPL simulator    ├→ bounded Channel<PriceTick> → one publisher worker → ZeroMQ
Bond simulator    ─┘
```

Current channel policy:

```csharp
Channel.CreateBounded<PriceTick>(
    new BoundedChannelOptions(100)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });
```

Why bounded?

An unbounded market-data backlog can become actively harmful. If the publisher falls behind, processing thousands of stale ticks later is less useful than retaining fresher state.

Why `DropOldest`?

```text
market data priority
= freshness over stale backlog
```

This policy would be inappropriate for durable business events such as `TradeCaptured`.

---

# 141. Channel Producer/Consumer and `await foreach`

Producer:

```text
creates PriceTick
writes to ChannelWriter<PriceTick>
```

Consumer:

```text
reads ChannelReader<PriceTick>
publishes each tick
```

The publisher uses async enumeration:

```csharp
await foreach (
    var tick in reader.ReadAllAsync(cancellationToken))
{
    publisher.Publish(tick);
}
```

When no tick is available, the async enumeration suspends while waiting for the next item. It does not need a CPU-spinning polling loop.

The actual wait is hidden in the asynchronous `MoveNext`/channel read machinery. A diagnostic line printed after processing a tick should therefore say something like:

```text
processed tick; requesting next PriceTick
```

rather than claiming that the `Console.WriteLine` itself is the wait point.

---

# 142. Task Is Not Thread

A recurring learning point from Phase 3A:

```text
Task
≠
Thread
```

A `Task` represents an operation and its eventual completion. An async method can suspend and later continue without owning one dedicated thread for its whole lifetime.

`Task.Run`, by contrast, queues synchronous work to the ThreadPool.

Use `Task.Run` only when there is a reason to move synchronous/blocking work away from the caller; do not use it merely because a method returns `Task`.

---

# 143. NetMQ Socket Thread Ownership

NetMQ sockets should not casually be shared across arbitrary threads.

The current design follows one-socket-owner principles:

```text
PriceTickPublisherWorker
    creates PublisherSocket
    uses PublisherSocket
    disposes PublisherSocket
```

and:

```text
PriceTickSubscriberWorker
    creates SubscriberSocket
    uses SubscriberSocket
    disposes SubscriberSocket
```

The publisher worker uses `NetMQRuntime` to run the async channel-to-publisher operation while preserving the NetMQ execution context.

Current publisher worker design deliberately exposes synchronous:

```csharp
public void Run(CancellationToken cancellationToken)
```

rather than pretending to be async when `NetMQRuntime.Run` occupies the caller until the publishing operation completes.

---

# 144. Cancellation Semantics

`CancellationTokenSource.Cancel()` means:

> Request cancellation.

It does not mean:

> All tasks have already stopped.

A simulator can currently be inside:

```csharp
await Task.Delay(delayMilliseconds, cancellationToken);
```

Cancellation can therefore throw `OperationCanceledException` before the loop condition is checked again.

This is why observing a final cleanup path is more reliable than assuming the `while` condition will visibly become false first.

The test/app lifecycle distinction is:

```text
Cancel()
= signal stop

await task / WaitAsync(...)
= wait until it has actually stopped
```

---

# 145. `OperationCanceledException` Filter

A useful pattern introduced during the simulator lifecycle work:

```csharp
catch (OperationCanceledException)
    when (cancellationTokenSource.IsCancellationRequested)
{
    ...
}
```

The `when` part is an exception filter.

It means:

> Catch `OperationCanceledException` here only when this application's cancellation was actually requested.

This prevents every possible `OperationCanceledException` from automatically being treated as expected shutdown.

---

# 146. ZeroMqPriceSubscriber

PricingService has a transport adapter that:

```text
connects to endpoint
subscribes to topics
receives multipart strings
validates frame count
JSON-deserializes PriceTick
validates topic == payload symbol
```

It uses a timed `TryReceive` rather than an infinite blocking receive.

The timeout is about shutdown responsiveness when there is no market data. It is not added trading latency. If a tick arrives immediately, receive returns immediately rather than waiting for the full timeout.

---

# 147. PriceTickSubscriberWorker

The subscriber worker owns the continuous ingestion loop:

```text
TryReceive
    ↓
PriceTick
    ↓
MarketQuoteCache.Update
    ↓
repeat
```

This class separates transport ingestion from the gRPC service. `PricingGrpcService` does not need to know anything about NetMQ sockets or JSON framing.

This is an adapter/boundary separation:

```text
ZeroMQ transport details
    stay in MarketData classes

pricing query contract
    stays in PricingGrpcService
```

---

# 148. BackgroundService Integration

PricingService uses a hosted background service so the subscriber starts and stops with the ASP.NET host.

Current shape:

```csharp
protected override Task ExecuteAsync(
    CancellationToken cancellationToken)
{
    return Task.Run(
        () => worker.Run(cancellationToken),
        cancellationToken);
}
```

Why `Task.Run` here?

The worker is synchronous and repeatedly calls a timed synchronous receive. Calling `worker.Run` directly inside `ExecuteAsync` would keep the call stack inside that synchronous loop instead of returning a lifetime `Task` to the host during startup.

`Task.Run` queues the synchronous long-running worker to the ThreadPool and returns a `Task` representing that work to the host.

This is a pragmatic bridge around the current synchronous NetMQ receive API. If the worker later becomes genuinely async, this hosting code can be revisited.

Project naming preference established during this phase:

> Use `CancellationToken cancellationToken` consistently rather than renaming the framework parameter to `stoppingToken`.

---

# 149. Configuration

The ZeroMQ subscriber endpoint belongs in PricingService configuration rather than being hard-coded into the worker.

Nested configuration can be read as:

```csharp
builder.Configuration.GetValue<string>(
    "MarketData:Endpoint")
```

or through the indexer:

```csharp
builder.Configuration["MarketData:Endpoint"]
```

Both are valid. `GetValue<string>` makes the expected target type explicit.

Current configured local endpoint:

```text
tcp://localhost:5555
```

---

# 150. Public Sealed Implementation-Class Preference

Project preference established during Phase 3A:

> Prefer `public sealed` implementation classes rather than `internal sealed`, unless there is a specific architectural reason to hide the type.

Examples include:

```csharp
public sealed class MarketQuoteCache
public sealed class ZeroMqPriceSubscriber
public sealed class PriceTickSubscriberWorker
public sealed class PriceTickSubscriberHostedService
public sealed class ZeroMqPricePublisher
public sealed class PriceTickPublisherWorker
public sealed class InstrumentPriceSimulator
public sealed class MarketDataSimulatorApplication
```

`sealed` communicates that inheritance is not part of the intended extension mechanism. Composition/interfaces remain preferred for varying behaviour.

---

# 151. Nullable Reference Types and `PriceTick?`

`PriceTick` is a reference type, so at runtime a reference can physically be null. Nullable-reference annotations describe intended contracts to the compiler.

```csharp
PriceTick tick;
```

means:

> This reference is intended to be non-null.

```csharp
PriceTick? tick;
```

means:

> Null is an expected valid state.

For a receive or cache lookup, no tick may exist yet, so `PriceTick?` is appropriate.

This does not change CLR reference storage. It changes compiler analysis and documents intent.

---

# 152. `[NotNullWhen(true)]`

`MarketQuoteCache.TryGet` has a boolean result correlated with an out value.

Business contract:

```text
true  → PriceTick exists
false → no PriceTick exists
```

Compiler-friendly contract:

```csharp
public bool TryGet(
    string symbol,
    [NotNullWhen(true)] out PriceTick? tick)
```

`[NotNullWhen(true)]` tells nullable-flow analysis:

> If this method returns true, treat `tick` as non-null after the call.

This is better than sprinkling the null-forgiving operator `!` at callers because it documents the real API contract once, at the source.

---

# 153. `Action` and Exception Assertions

An `Action` is a delegate that holds executable code taking no arguments and returning `void`.

Example:

```csharp
Action receive = () =>
{
    subscriber.TryReceive(...);
};
```

Creating the `Action` does not execute the body. Execution happens later when the delegate is invoked.

This is useful for exception assertions because FluentAssertions needs to receive the code before executing it:

```csharp
receive.Should()
    .Throw<InvalidOperationException>();
```

Conceptually the assertion library does:

```text
try
    invoke receive()
catch expected exception
    pass
otherwise
    fail
```

For asynchronous code the equivalent pattern is usually `Func<Task>` plus `ThrowAsync<TException>()`.

---

# 154. ZeroMQ Integration-Test Strategy

PUB/SUB startup is asynchronous, so a naive test can be flaky:

```text
create subscriber
immediately send once
expect guaranteed receive
```

The first tick may be lost while connection/subscription propagation is still catching up.

Tests therefore retry delivery within a bounded attempt/deadline rather than assuming the first send must succeed.

This tests the real requirement:

> Once the PUB/SUB connection is active, can the subscriber receive and correctly decode the market-data protocol?

Tests use a real NetMQ publisher for the transport boundary rather than mocking ZeroMQ away.

---

# 155. Why `Task.Run` Appears in the Subscriber Worker Test

`PriceTickSubscriberWorker.Run` is synchronous and long-running. If the test called it directly:

```csharp
worker.Run(cancellationToken);
```

the test could not proceed to publish a message until the worker returned, which normally happens only on cancellation.

The test therefore uses:

```csharp
var workerTask = Task.Run(
    () => worker.Run(cancellationToken));
```

so the test can concurrently:

```text
run subscriber worker
and
publish a test tick
```

This is test orchestration, not a statement that every production component should be wrapped in `Task.Run`.

After cancellation, waiting for `workerTask` ensures the background work has actually stopped before the test exits.

A bounded cleanup wait such as:

```csharp
await workerTask.WaitAsync(
    TimeSpan.FromSeconds(2));
```

also protects the test suite from hanging forever if cancellation becomes broken.

---

# 156. Console QuickEdit / Selection Behaviour

During runtime verification on Windows, clicking/selecting text in a classic console window can pause console output due to QuickEdit/selection behaviour.

Because tick publishing/receiving currently logs frequently with `Console.WriteLine`, the application can appear to have stopped even though the issue is console I/O/selection rather than ZeroMQ failure.

Pressing Escape exits selection mode. Per-tick console output is useful for learning but should not be treated as production market-data telemetry.

---

# 157. Decimal vs Double in the Pricing Path

Internal market-data contract:

```text
decimal
```

Current protobuf pricing fields:

```text
double
```

`decimal` is preferred internally for prices, quantities, notionals and P&L because it supports base-10 financial arithmetic without the usual binary floating-point representation issue.

`double` is binary floating-point and is appropriate for many scientific/statistical workloads, but values such as `0.1` are not exactly representable in binary.

Current architecture rule:

> Keep financial calculations in decimal. Convert explicitly to double only at the current protobuf transport boundary.

That is why `GetPrice` contains explicit casts:

```csharp
var bid = (double)priceTick.Bid;
var ask = (double)priceTick.Ask;
```

and tests use approximate comparisons for protobuf `double` values.

A future exact-decimal wire representation could use an integer plus scale, but there is no need to redesign the protobuf contract yet.

---

# 158. Live Runtime Verification — EURUSD Buy

Verified request correlation ID:

```text
phase-3a-eurusd-buy-001
```

PricingService returned:

```text
EURUSD
Bid = 1.0893
Ask = 1.0895
Mid = 1.0894
```

For a Buy, the executable side is Ask:

```text
ExecutionPrice = 1.0895
```

This proved that the trade path could consume a quote originating from the live ZeroMQ feed rather than the old hard-coded dictionaries.

---

# 159. Live Runtime Verification — EURUSD Sell and Realised P&L

Verified correlation ID:

```text
phase-3a-eurusd-sell-001
```

PricingService returned:

```text
Bid = 1.0846
Ask = 1.0848
Mid = 1.0847
```

Sell execution rule:

```text
Sell → Bid
```

The earlier Buy was:

```text
Buy 100,000 @ 1.0895
```

Closing Sell:

```text
Sell 100,000 @ 1.0846
```

Price difference:

```text
1.0846 - 1.0895
= -0.0049
```

FX realised P&L:

```text
100,000 × -0.0049
= -490 USD
```

Observed `RealisedPnl`:

```text
-490.00000000
```

This verified the complete chain:

```text
live ZeroMQ Bid
→ gRPC price
→ Sell execution
→ TradeCaptured
→ PositionService
→ realised P&L
```

---

# 160. Live Runtime Verification — AAPL

Verified correlation ID:

```text
phase-3a-aapl-buy-001
```

PricingService returned:

```text
Bid = 203.75
Ask = 204.25
Mid = 204.00
```

For the Buy:

```text
ExecutionPrice = Ask = 204.25
```

For quantity `10`, expected equity trade value is:

```text
10 × 204.25
= 2,042.50 USD
```

This verified that the market-data transport and PricingService cache are not FX-specific.

---

# 161. Live Runtime Verification — Bond

Verified correlation ID:

```text
phase-3a-bond-buy-001
```

PricingService returned:

```text
GB00TEST1234
Bid = 100.20
Ask = 100.30
Mid = 100.25
```

For the Buy:

```text
ExecutionPrice = Ask = 100.30
```

For nominal quantity `100,000`, the existing simplified percentage-price bond rule gives:

```text
trade value
= quantity × price / 100
= 100,000 × 100.30 / 100
= 100,300 GBP
```

The important architecture point is:

```text
same live pricing infrastructure
+
different asset-specific trade-value rule
```

---

# 162. Phase 3A Tests Added / Changed

Market-data tests now cover important layers independently:

```text
MarketQuoteCache unit tests
    - update stores a tick
    - later update replaces same-symbol tick
    - missing symbol returns false

ZeroMqPriceSubscriber integration tests
    - real NetMQ multipart message is received/deserialized
    - topic/payload symbol mismatch is rejected

PriceTickSubscriberWorker integration test
    - received tick updates MarketQuoteCache
    - cancellation stops worker

PricingGrpcService tests
    - seeded cache returns expected live Bid/Ask/Mid
    - symbol normalization still works
    - missing market data returns Unavailable
    - empty symbol remains InvalidArgument
    - correlation ID path still returns price
    - equity and bond quotes work through same cache path
```

Random simulator movement itself should not be unit-tested for a particular sequence because randomness/order is not the business contract.

---

# 163. Market Data Known Limitations After Phase 3A

Current implementation intentionally does not yet provide:

```text
sequence numbers
gap recovery
source/venue identity
bid/ask sizes
quote-age rejection
stale-feed alarms
heartbeats
reconnection policy beyond current NetMQ behaviour
market status / halted state
multiple publishers / LP aggregation
best bid/offer aggregation
persistent tick history
high-frequency telemetry/metrics
```

These are future production concerns. They should be added when the next business requirement needs them rather than pre-built speculatively.

A particularly important future rule will be quote freshness. `MarketQuoteCache` currently answers with the most recently received tick even if it has become old. Before external/live execution, PricingService should likely enforce a maximum quote age or market-data health state.

---

# 164. Phase 3B — Working Limit Orders Starting Point

Phase 3A gives us the changing market needed for genuine limit-order behaviour.

Business rules to preserve:

```text
Market Buy
execute now at Ask

Market Sell
execute now at Bid
```

Limit rules:

```text
Buy Limit
execute only when Ask <= LimitPrice

Sell Limit
execute only when Bid >= LimitPrice
```

Example:

```text
Buy AAPL Limit 200.00

Ask 204.25
→ rest

Ask 202.00
→ rest

Ask 199.75
→ eligible to trigger
```

The next architecture discussion should follow the established learning sequence:

```text
business/trading meaning
→ mathematical/market rule
→ architecture/design decision
→ C# implementation
```

The key question is no longer how to obtain moving prices. It is which component owns a working order while it waits, how that component observes price changes, and how it guarantees that one limit order is not triggered twice.

---

This document should remain the single cumulative source of architecture notes as the platform evolves.

---

# 165. Phase 3B — Working Limit Orders: Business Meaning

A Limit order is not the same thing as a Market order.

Market-order rule:

```text
Buy  → execute immediately at Ask
Sell → execute immediately at Bid
```

Limit-order rule:

```text
Buy Limit  → execute only when Ask <= LimitPrice
Sell Limit → execute only when Bid >= LimitPrice
```

The limit price is an instruction boundary, not necessarily the execution price.

Example:

```text
Buy LimitPrice = 1.0850
Current Ask    = 1.0847

1.0847 <= 1.0850
→ order is executable
→ customer should receive 1.0847, not 1.0850
```

For a Sell:

```text
Sell LimitPrice = 1.0850
Current Bid     = 1.0852

1.0852 >= 1.0850
→ order is executable
→ execution price = 1.0852
```

Important distinction:

```text
LimitPrice     = customer's worst acceptable price
ExecutionPrice = actual executable market price that satisfied the limit
```

This distinction is critical because TradeCapture must not re-price the order after the Saga has already found an executable quote. A later re-price could move beyond the customer's limit.

---

# 166. LimitPrice Added Through the Submission Path

The Gateway already had `LimitPrice` on `SubmitOrderRequest` and `SubmitOrderCommand`. Phase 3B completed the flow through messaging and persistence.

The transport meaning is:

```text
Market order → LimitPrice = null
Limit order  → LimitPrice is required and > 0
```

`TradingApp.Contracts.Commands.SubmitOrder` carries:

```csharp
public decimal? LimitPrice { get; set; }
```

The Gateway handler forwards it into the NServiceBus `SubmitOrder` command.

The Order entity now persists it as:

```csharp
public decimal? LimitPrice { get; set; }
```

EF precision:

```csharp
entity.Property(e => e.LimitPrice)
    .HasPrecision(18, 8);
```

The column is nullable because Market orders have no limit price.

A SQL Server migration was created and applied for this column.

---

# 167. Validation Framework Refactor for Cross-Field Rules

The existing validation framework was kept generic rather than hard-coding a `SubmitOrderCommand`-specific validator into shared infrastructure.

The old broad name:

```text
IObjectValidationRule<T>
```

was renamed to the clearer:

```csharp
public interface IValidationRule<T>
{
    string? Validate(T value);
}
```

`FieldRule<T, TValue>` now implements `IValidationRule<T>`.

A generic cross-field adapter was introduced:

```csharp
public sealed class CrossFieldRule<T, TValue>
    : IValidationRule<T>
{
    private readonly Func<T, TValue> selector;
    private readonly IValidationRule<TValue> rule;

    public CrossFieldRule(
        Func<T, TValue> selector,
        IValidationRule<TValue> rule)
    {
        this.selector = selector;
        this.rule = rule;
    }

    public string? Validate(T value)
    {
        var selectedValue = this.selector(value);
        return this.rule.Validate(selectedValue);
    }
}
```

This allows a validator to package multiple related fields into one value, for example:

```text
(OrderType, LimitPrice)
```

and hand that tuple to a business-specific rule.

The command-specific validation was moved close to the command under the `SubmitOrder` application area rather than mixed into generic validation infrastructure.

The limit-order rule enforces:

```text
OrderType = Limit + LimitPrice = null  → invalid
OrderType = Limit + LimitPrice <= 0    → invalid
OrderType = Limit + LimitPrice > 0     → valid
```

Invalid `OrderType` parsing is left to the existing enum-string rule so the cross-field rule does not produce duplicate/confusing errors.

---

# 168. C# Generic Variance Note — `in T`

`IValidationRule<T>` intentionally does not currently use:

```csharp
IValidationRule<in T>
```

`in T` would make `T` contravariant: a rule that can consume a broader type could be assigned where a narrower consumer is expected.

That is useful framework knowledge, but the current validator uses exact generic types and does not need that substitution flexibility. The simpler invariant form is easier to understand and sufficient for the current platform.

---

# 169. NServiceBus Extension-Method Testing Lesson

The Gateway tests hit an important Moq limitation.

This setup does not work:

```csharp
transactionalSession.Setup(x => x.Send(
    It.IsAny<SubmitOrder>(),
    It.IsAny<CancellationToken>()))
```

because the two-parameter `Send(message, cancellationToken)` is an extension method.

Moq cannot intercept extension methods because they are static methods, not real members on `ITransactionalSession`.

The working setup mocks the real underlying interface method:

```csharp
transactionalSession.Setup(x => x.Send(
    It.IsAny<object>(),
    It.IsAny<SendOptions>(),
    It.IsAny<CancellationToken>()))
```

and captures the actual outgoing command through the callback.

Memory rule:

```text
production extension method
→ convenient syntax
→ internally calls real interface method
→ mock the real interface method, not the extension
```

This allowed the test to verify that a positive `LimitPrice` really crosses the Gateway messaging boundary.

---

# 170. OrderAccepted Carries LimitPrice

`OrderAccepted` now carries:

```csharp
public decimal? LimitPrice { get; set; }
```

OrderService publishes:

```csharp
LimitPrice = order.LimitPrice
```

This preserves the customer's instruction downstream.

However, TradeCapture no longer immediately executes Limit orders from `OrderAccepted`. Market and Limit responsibilities are now deliberately separated.

---

# 171. Phase 3B Ownership Decision — OrderService Owns the Limit Lifecycle

An early design temporarily considered having `TradeCaptureService` send `StartLimitOrder` back to OrderService. That was rejected because it created an unnecessary round trip:

```text
OrderService
→ TradeCaptureService
→ OrderService
```

The corrected ownership is:

```text
OrderService
    owns order lifecycle and long-running Limit-order state

TradeCaptureService
    owns trade creation once an execution decision already exists
```

Therefore:

```text
Risk approved Market order
→ publish OrderAccepted
→ TradeCapture executes immediately

Risk approved Limit order
→ publish OrderAccepted as a business fact
→ SendLocal StartLimitOrder
→ OrderService Saga manages the waiting lifecycle
```

`TradeCaptureService.OrderAcceptedHandler` now bypasses Limit orders:

```text
OrderType = Limit
→ return
→ no PricingService call
→ no immediate Trade
```

A dedicated test protects this behavior.

---

# 172. StartLimitOrder Command

A dedicated command starts the long-running Limit lifecycle:

```text
StartLimitOrder
```

`Working` was deliberately not put into the command name because Working is a state, not the command intent.

The command carries the durable instruction context needed by the Saga:

```text
OrderId
ClientId
Symbol
Side
Quantity
LimitPrice
RiskDecisionId
CorrelationId
```

`LimitPrice` is non-nullable here because the command should only be produced for a valid Limit order.

The command implements NServiceBus `ICommand` and the existing correlated-message convention/interface used by the platform.

---

# 173. Limit Order Saga

The executable Saga class is named:

```text
LimitOrderSagaHandler
```

and lives with the other handlers because this codebase organizes message-processing behavior under `Handlers`.

Its persisted state is separated into:

```text
LimitOrderSagaData
```

Conceptual distinction:

```text
LimitOrderSagaHandler
= behavior / message processing

LimitOrderSagaData
= durable long-running process state
```

The Saga starts from:

```csharp
IAmStartedByMessages<StartLimitOrder>
```

and uses `OrderId` as the business correlation key.

The correlation mapping is conceptually:

```text
LimitOrderSagaData.OrderId
↔ StartLimitOrder.OrderId
↔ TradeCaptured.OrderId
```

The initial `Handle(StartLimitOrder...)` normally runs once for a given Saga instance and populates the durable state.

---

# 174. Saga Timeouts for Limit-Price Rechecking

The Saga implements:

```csharp
IHandleTimeouts<CheckOrderLimitPrice>
```

The timeout type was named `CheckOrderLimitPrice` so `LimitPrice` remains together as a domain term.

The first timeout is requested from the Saga start handler:

```csharp
await RequestTimeout<CheckOrderLimitPrice>(
    context,
    TimeSpan.FromSeconds(1));
```

This is intentionally different from `Task.Delay`.

`Task.Delay` would keep an asynchronous operation alive waiting. A Saga timeout instead persists the workflow and lets the handler finish:

```text
Handle(StartLimitOrder)
→ request timeout
→ handler completes
→ Saga state persisted
→ no thread/task waits for the whole order lifetime
→ NServiceBus wakes the Saga later
```

When the timeout fires:

```text
get current quote
→ evaluate Buy/Sell limit rule
→ if not executable, request another timeout
→ if executable, trigger execution
```

A Limit order may become executable on the very first timeout. That is expected. If the first quote already satisfies the limit, the observed lifecycle can look like:

```text
Started
→ roughly one second later
→ Triggered
```

without a visibly long Working period.

---

# 175. LimitOrderExecutionEvaluator

The marketability rule was isolated into a dedicated evaluator owned by OrderService because OrderService owns the working Limit lifecycle.

Its job is only:

```text
SHOULD this order execute at this quote?
```

It does not own persistence, messaging, or quote retrieval.

The rule is:

```text
Buy  → ask <= limitPrice
Sell → bid >= limitPrice
```

The evaluator takes primitive `bid` and `ask` values rather than depending on TradeCaptureService's `PriceQuote`. This avoids a project dependency from OrderService to TradeCaptureService.

An invalid `OrderSide` throws `ArgumentOutOfRangeException` and is covered by a dedicated test.

---

# 176. OrderService Pricing Client

The Saga must query the current quote, so OrderService now has its own PricingService gRPC client adapter.

OrderService does not reference TradeCaptureService to reuse its implementation.

Correct dependency shape:

```text
OrderService --------┐
                     ├→ PricingService gRPC contract
TradeCaptureService -┘
```

Incorrect shape avoided:

```text
OrderService
→ TradeCaptureService implementation
→ PricingService
```

OrderService contains its own:

```text
IPricingClient
GrpcPricingClient
PriceQuote
```

The gRPC contract uses `double`, while internal financial values use `decimal`, so conversion occurs at the transport boundary.

---

# 177. Limit Trigger Decision

On each `CheckOrderLimitPrice` timeout:

```text
PricingService.GetPrice(Symbol)
→ Bid / Ask
→ LimitOrderExecutionEvaluator.CanExecute(...)
```

If false:

```text
Saga remains Working
→ request another timeout
```

If true:

```text
Buy  → ExecutionPrice = Ask
Sell → ExecutionPrice = Bid
→ Saga becomes Triggered
→ send ExecuteLimitOrder
```

The Buy/Ask and Sell/Bid execution-price rule is currently duplicated as a tiny local expression in the Saga rather than forcing a broad shared-kernel refactor.

An attempted move of `OrderSide`/`ExecutionPriceCalculator` into `TradingApp.SharedKernel` created too much dependency churn. That refactor was deliberately reverted. A small temporary duplication is preferable to destabilizing the solution during Phase 3B.

Potential future cleanup: move shared trading primitives into the correct domain layer in a dedicated refactor after the working-order architecture is stable.

---

# 178. ExecuteLimitOrder Command

Once a Limit order is marketable, the Saga sends:

```text
ExecuteLimitOrder
```

This is a command because it instructs a single logical owner to do something.

It carries:

```text
OrderId
ClientId
Symbol
Side
Quantity
LimitPrice
ExecutionPrice
RiskDecisionId
ExecutedAt
CorrelationId
```

`RiskDecisionId` is preserved through the full lifecycle so the eventual Trade can remain traceable back to the risk approval that authorized the order.

`ExecutedAt` records the time the Saga observed the executable condition in the current simplified model.

The platform does not yet model a separate external venue acknowledgement/fill timestamp. When FIX / real venue execution is introduced, this timestamp model will likely become richer.

---

# 179. NServiceBus Commands vs Events and Routing

A useful memory rule:

```text
Command = "DO something"
→ one logical owner
→ Send(...)
→ explicit route required

Event = "something HAPPENED"
→ zero, one, or many subscribers
→ Publish(...)
→ subscription-based
```

For example:

```text
ExecuteLimitOrder
→ command
→ OrderService Saga tells TradeCaptureService to execute
```

Therefore OrderService configures:

```csharp
routing.RouteToEndpoint(
    typeof(ExecuteLimitOrder),
    EndpointNames.TradeCaptureService);
```

`EndpointNames.TradeCaptureService` resolves to the endpoint name used by TradeCaptureService, e.g. `TradeCapture.Service`.

By contrast:

```text
OrderAccepted
TradeCaptured
```

are events. Publishers do not need to know every subscriber endpoint.

The old `CaptureTrade` route was removed because the command was historical dead code in the current architecture.

---

# 180. NServiceBus Message Marker Interfaces

A runtime startup error exposed another important rule:

```text
Cannot configure routing ... because it is not considered a message
```

NServiceBus routing only accepts types that are recognized as messages through marker interfaces or configured conventions.

The new command contracts therefore implement the same message conventions as the rest of the platform, e.g.:

```text
ICommand
ICorrelatedMessage
```

Memory rule:

```text
ICommand → Send + routing
IEvent   → Publish + subscriptions
```

---

# 181. TradeCaptureRequest — Internal Common Execution Shape

Market and Limit handlers shared nearly the entire trade-capture pipeline. The only material difference was how `ExecutionPrice` was obtained.

Rather than passing a long list of primitive parameters or coupling the common processor directly to `OrderAccepted`, an internal model was introduced:

```text
TradeCaptureRequest
```

It contains the common execution facts:

```text
OrderId
ClientId
Symbol
Side
OrderType
Quantity
ExecutionPrice
RiskDecisionId
ExecutedAt
CorrelationId
```

`LimitPrice` is deliberately not required by the generic trade-capture processor. The limit condition has already been enforced before the processor is invoked. The processor needs the actual execution price.

---

# 182. TradeCaptureProcessor

The common body of TradeCapture was extracted into:

```text
TradeCaptureProcessor
```

Its responsibility is:

```text
already-decided ExecutionPrice
→ duplicate/idempotency check
→ ReferenceData lookup
→ asset-specific NotionalCalculatorResolver
→ calculate trade value/notional
→ persist Trade
→ publish TradeCaptured
```

It deliberately does not:

```text
call PricingService
choose Buy/Ask vs Sell/Bid
check a LimitPrice
manage working-order state
```

That separation lets both Market and Limit execution reuse the exact same durable trade-capture path.

The processor is registered as scoped because it depends on scoped message-processing persistence such as `IUnitOfWork` / DbContext.

---

# 183. Market Order Handler After Refactor

`OrderAcceptedHandler` is now much smaller for Market orders:

```text
OrderAccepted Market
→ optional early duplicate check
→ PricingService.GetPrice
→ ExecutionPriceCalculator.GetExecutionPrice
→ build TradeCaptureRequest
→ TradeCaptureProcessor.CaptureAsync
```

Market orders continue to execute immediately.

Limit orders return early from this handler and do not call PricingService or capture a Trade from the `OrderAccepted` event.

---

# 184. Duplicate-Trade Protection After Refactor

A test regression correctly exposed that moving duplicate detection solely into `TradeCaptureProcessor` caused Market duplicates to call PricingService unnecessarily before the processor discovered the duplicate.

The final design keeps two checks with different purposes:

```text
OrderAcceptedHandler early check
→ optimization
→ avoid unnecessary PricingService call for known duplicate

TradeCaptureProcessor internal check
→ actual idempotency protection
→ protects both Market and Limit execution paths
```

The internal processor check remains essential because every caller must be protected even if it did not perform an early optimization check.

---

# 185. ExecuteLimitOrderHandler

`TradeCaptureService` now handles `ExecuteLimitOrder` through a small dedicated handler.

Its responsibility is simply:

```text
ExecuteLimitOrder
→ map to TradeCaptureRequest
→ TradeCaptureProcessor.CaptureAsync
```

It does not call PricingService again.

That is critical for the business guarantee:

```text
Saga evaluated quote satisfies LimitPrice
→ Saga freezes that ExecutionPrice in ExecuteLimitOrder
→ TradeCapture persists exactly that ExecutionPrice
```

No second quote lookup can move the execution beyond the customer's limit.

---

# 186. RiskDecisionId and Execution Audit Lineage

The Limit path preserves the risk decision through:

```text
SubmitOrderHandler
→ StartLimitOrder
→ LimitOrderSagaData
→ ExecuteLimitOrder
→ TradeCaptureRequest
```

This creates a clean audit lineage:

```text
risk approved
→ order accepted
→ limit order waited
→ quote condition satisfied
→ execution command created
→ trade captured
```

Future trade/audit models may choose to persist `RiskDecisionId` directly on the Trade entity if full database-level lineage is required.

---

# 187. Saga Lifecycle Completion via TradeCaptured

After the Saga sends `ExecuteLimitOrder`, it must not complete immediately. Sending a command does not prove that the trade was actually captured.

The Saga also handles the existing `TradeCaptured` event.

Lifecycle:

```text
Working
→ Triggered
→ ExecuteLimitOrder sent
→ TradeCaptureService persists Trade
→ TradeCaptured published
→ same Saga found by OrderId
→ Filled
→ MarkAsComplete()
```

`MarkAsComplete()` tells NServiceBus that the long-running process has finished and the Saga state can be removed by Saga persistence.

This also gives `ConfigureHowToFindSaga` a meaningful second-message correlation use case:

```text
StartLimitOrder(OrderId = X)
→ creates Saga X

TradeCaptured(OrderId = X)
→ correlates back to Saga X
```

Direct unit calls to `Handle(TradeCaptured...)` test the state-transition behavior but do not by themselves prove NServiceBus runtime correlation. A fuller Saga integration test can be added later if the testing harness warrants it.

---

# 188. Order Entity Lifecycle Now Mirrors the Saga

Saga workflow state alone is not enough. The `Orders` table is the durable business record that other components would query.

The persisted Order lifecycle is now kept aligned with the Saga:

```text
PendingRisk
→ Accepted
→ Working
→ Triggered
→ Filled
```

Rejected remains a terminal risk path.

Transitions:

```text
SubmitOrder risk approved
→ Order.Status = Accepted

StartLimitOrder handled
→ Order.Status = Working

Limit condition satisfied
→ Order.Status = Triggered

TradeCaptured received
→ Order.Status = Filled
→ Saga MarkAsComplete()
```

The Saga and Order entity are intentionally both updated because they serve different purposes:

```text
Saga status
→ workflow/process state used by NServiceBus

Order status
→ durable business state queried by the trading system
```

---

# 189. OrderStatus Enum

The growing number of magic status strings was replaced with a strongly typed domain enum:

```csharp
public enum OrderStatus
{
    PendingRisk,
    Accepted,
    Working,
    Triggered,
    Filled,
    Rejected
}
```

`Order.Status` now uses `OrderStatus` instead of `string`.

EF keeps the database human-readable with string conversion:

```csharp
entity.Property(e => e.Status)
    .HasConversion<string>()
    .HasMaxLength(30);
```

The existing SQL column was already `nvarchar(30)`, so after keeping the same width the generated `UseOrderStatusEnum` migration had an empty `Up()` method.

The empty migration was kept and applied because it records a real model evolution even though no physical SQL DDL change was required.

EF migration reminder:

```text
Update-Database
→ checks __EFMigrationsHistory
→ applies every pending migration in order
```

You normally do not apply each pending migration manually one by one.

---

# 190. LimitOrderSagaStatus Enum

Saga status strings were also replaced with a Saga-specific enum:

```text
LimitOrderSagaStatus.Working
LimitOrderSagaStatus.Triggered
LimitOrderSagaStatus.Filled
```

This enum is intentionally separate from `OrderStatus`.

Even though some names currently match, they represent different concepts:

```text
LimitOrderSagaStatus
→ long-running workflow state

OrderStatus
→ business entity state
```

They should not be forced into one enum merely because the current values overlap.

---

# 191. Logging Policy for the Limit Saga

Per-timeout information logging was deliberately avoided because a working order can remain alive for minutes or hours and a one-second polling loop could create thousands of low-value log entries.

Preferred permanent Information logs are meaningful lifecycle transitions:

```text
Limit order started
Limit order triggered
Limit order filled
```

Per-poll information, if temporarily useful during development, should be Debug-level rather than permanent Information-level noise.

This keeps production logs useful for tracing business state transitions rather than internal polling mechanics.

---

# 192. Saga Timeout Test Lesson

`NServiceBus.Testing` exposes timeout requests through messaging-related collections.

An assertion such as:

```text
SentMessages should be empty
```

was too broad for a Working order because `RequestTimeout<CheckOrderLimitPrice>` itself appears as sent timeout-related activity.

The correct behavioral assertion is narrower:

```text
Working order
→ no ExecuteLimitOrder command sent
→ one timeout scheduled
```

versus:

```text
Triggered order
→ ExecuteLimitOrder sent
→ no further timeout scheduled
```

This is a general testing lesson: assert the business message you care about, not that the entire infrastructure collection is empty.

---

# 193. Limit Saga Test Starting-State Lesson

A timeout unit test calls `Timeout(...)` directly. It therefore must seed the state that would exist after the real Saga start handler has already run.

Correct timeout test precondition:

```text
SagaData.Status = Working
Order.Status    = Working
```

Not:

```text
SagaData.Status = Working
Order.Status    = Accepted
```

The separate Saga start test proves:

```text
Accepted → Working
```

The timeout theory then proves:

```text
Working → Working
or
Working → Triggered
```

The TradeCaptured test proves:

```text
Triggered → Filled
```

This keeps each test focused on the lifecycle transition it actually invokes.

---

# 194. Phase 3B End-to-End Runtime Verification

A real EURUSD Buy Limit was executed end to end.

Correlation ID:

```text
phase-3b-eurusd-limit-buy-001
```

Observed Trade row:

```text
OrderType        = Limit
Quantity         = 100000.0000
Price            = 1.08260000
Notional         = 108260.0000
InstrumentId     = 11111111-1111-1111-1111-111111111111
AssetClass       = Fx
NotionalCurrency = USD
Status           = Captured
```

Notional verification:

```text
100,000 × 1.0826
= 108,260 USD
```

The important result is that the Trade persisted the actual market execution price chosen by the Saga, not simply the LimitPrice.

A later end-to-end run also verified that the persisted Order reached:

```text
Status = Filled
```

This confirms the complete durable path:

```text
Submit Limit order
→ risk approved
→ Accepted
→ StartLimitOrder
→ Working
→ timeout quote check
→ Triggered
→ ExecuteLimitOrder
→ Trade captured
→ TradeCaptured
→ Order Filled
→ Saga complete
```

---

# 195. Position CorrelationId Semantics

During the Phase 3B runtime check, the `Positions` table was observed to update `CorrelationId` to the latest trade correlation ID.

That matches the current model semantics:

```text
Position row
→ current-state snapshot
→ CorrelationId effectively means latest trade/update correlation ID
```

Historical audit is preserved in `PositionMovement` rows, where each movement should retain the correlation ID of the trade that caused that movement.

Current recommendation:

```text
keep current behavior
```

Potential future cleanup:

```text
Position.CorrelationId
→ rename to LastCorrelationId
```

if the codebase needs the meaning to be more explicit.

Do not lose the `PositionMovement` history; that remains the true per-change audit trail.

---

# 196. Phase 3B Test Coverage Summary

Phase 3B now has focused coverage for the major responsibilities:

```text
Gateway validation
- Limit + null LimitPrice rejected
- Limit + zero/non-positive LimitPrice rejected
- positive LimitPrice accepted and propagated

OrderService SubmitOrderHandler
- LimitPrice persisted
- OrderAccepted carries LimitPrice
- accepted Limit order starts the local Saga flow

TradeCapture OrderAcceptedHandler
- Market flow still captures
- Limit OrderAccepted does not capture immediately
- duplicate Market order avoids unnecessary pricing call

LimitOrderExecutionEvaluator
- Buy equality executes
- Buy above-limit Ask waits
- Sell equality executes
- Sell below-limit Bid waits
- unsupported side throws

LimitOrderSagaHandler
- StartLimitOrder populates Saga data
- Order transitions Accepted → Working
- Working timeout reschedules when not executable
- executable timeout transitions to Triggered
- ExecuteLimitOrder uses Ask for Buy / Bid for Sell
- no ExecuteLimitOrder is sent while still Working
- RiskDecisionId and execution timestamp are propagated
- TradeCaptured transitions Triggered → Filled

ExecuteLimitOrderHandler / TradeCaptureProcessor
- Limit execution uses supplied ExecutionPrice
- common ReferenceData/notional/persistence/TradeCaptured path reused
```

The goal remains to avoid tests that merely duplicate property assignments. Prefer tests that protect a business rule, service boundary, lifecycle transition, or idempotency guarantee.

---

# 197. Phase 3B Architectural Lessons

The most important design lessons from this phase are:

```text
1. OrderAccepted does not mean TradeExecuted.

2. A Limit order is a long-running process.

3. OrderService owns order lifecycle.

4. TradeCaptureService owns trade creation.

5. Raw market ticks should not be pushed through a durable Saga workflow.

6. Saga timeout is appropriate for durable periodic re-evaluation in the current learning architecture.

7. LimitPrice is an instruction; ExecutionPrice is the actual market fill price.

8. Do not re-price after the limit condition was satisfied unless the execution model explicitly supports a new marketability check.

9. Commands need explicit routing to their logical owner; events use subscriptions.

10. A shared processor should accept an internal execution model rather than a giant primitive parameter list or one specific integration message type.

11. Keep idempotency inside the common processor even if callers add an early optimization check.

12. Persisted Order status and Saga workflow status are related but conceptually distinct.
```

---

# 198. Current High-Level Platform Flow After Phase 3B

```text
HTTP SubmitOrder
      ↓
TradingGateway.Api
      ↓ NServiceBus SubmitOrder command
OrderService
      ↓
RiskService gRPC
      ↓
Risk approved
      ↓
Order persisted Accepted
      ↓
      ├─────────────────────────────────────────────┐
      │                                             │
      │ Market                                      │ Limit
      │                                             │
      ↓                                             ↓
Publish OrderAccepted                       SendLocal StartLimitOrder
      ↓                                             ↓
TradeCaptureService                         LimitOrderSagaHandler
OrderAcceptedHandler                               ↓
      ↓                                      Order = Working
PricingService gRPC                                ↓
      ↓                                      Saga timeout
live MarketQuoteCache                             ↓
      ↓                                      PricingService gRPC
Buy→Ask / Sell→Bid                                ↓
      ↓                                      CanExecute?
TradeCaptureRequest                         no ↙         ↘ yes
      ↓                                    timeout      Triggered
TradeCaptureProcessor                                        ↓
      ↓                                             ExecuteLimitOrder command
ReferenceDataService                                          ↓
      ↓                                             TradeCaptureService
NotionalCalculatorResolver                                    ↓
      ↓                                             ExecuteLimitOrderHandler
Trade persisted                                               ↓
      ↓                                             TradeCaptureProcessor
Publish TradeCaptured                                         ↓
      ↓                                             Trade persisted
PositionService                                               ↓
updates position                                      Publish TradeCaptured
                                                            ↓
                                                   LimitOrderSagaHandler
                                                            ↓
                                                     Order = Filled
                                                            ↓
                                                    MarkAsComplete()
```

---

# 199. Current Phase Status

Completed major milestones:

```text
Phase 2A  Position lifecycle
Phase 2B  Pricing gRPC + correlation
Phase 2C  RiskService / rejection flow
Phase 2D  ReferenceData + multi-asset notional
Phase 2E  realised P&L / multi-asset position behavior
Phase 3A  live ZeroMQ market data into PricingService
Phase 3B  working Limit orders with Saga lifecycle and execution
```

Phase 3B is functionally complete enough to commit as a milestone.

---

# 200. Recommended Next Phase — Phase 3C Unrealised P&L / Mark-to-Market

The next planned business capability is unrealised P&L using live market prices.

Current PositionService already has:

```text
NetQuantity
AveragePrice
RealisedPnl
UnrealisedPnl field
```

but unrealised P&L is not yet continuously driven by the live market feed.

Business rule for a simple position:

```text
Long position
Unrealised P&L ≈ NetQuantity × (MarkPrice - AveragePrice)

Short position
Unrealised P&L ≈ |NetQuantity| × (AveragePrice - MarkPrice)
```

The exact mark convention must be chosen deliberately:

```text
Mid?
Bid for long liquidation / Ask for short liquidation?
asset-class-specific mark?
```

Recommended teaching/design sequence:

```text
business meaning of mark-to-market
→ choose mark convention
→ asset-class formula
→ decide which service owns market-driven position revaluation
→ transport from Pricing/market data
→ update Position.UnrealisedPnl
→ publish/report PositionUpdated
```

Do not simply send every raw market tick through NServiceBus without first deciding the required durability/throughput semantics.

---

# 201. Later Planned Phases

After Phase 3C, the current roadmap remains approximately:

```text
Phase 3D
SignalR live UI updates

Phase 3E
Market watch
positions grid
realised/unrealised P&L displays
P&L charts and reports
```

Then execution/order-management maturity:

```text
Cancel order
Amend order
Limit-order expiry / TimeInForce
partial fills
execution reports
order fill quantity vs remaining quantity
multiple fills per order
venue/LP routing
FIX connectivity
```

Market-data maturity:

```text
sequence numbers
source / venue identity
quote age / stale-price checks
heartbeats
feed health
reconnection strategy
bid/ask sizes
multiple publishers
LP aggregation
best bid/offer
market status / halts
historical tick storage where justified
```

Fixed-income maturity:

```text
accrued interest
clean vs dirty price
day-count conventions
yield calculations
DV01 / PV01
duration / convexity
curve-based pricing
```

Risk / Greeks learning path:

```text
Delta
Gamma
Vega
Theta
Rho
DV01/PV01
scenario shocks
VaR
portfolio aggregation
limits and breaches
```

When these are introduced, keep using the preferred learning structure:

```text
business/trading meaning
→ mathematical/market rule
→ architecture/design decision
→ C# implementation
```

---

# 202. Future Saga / Limit-Order Improvements

The current one-second Saga timeout is a learning-friendly implementation, not necessarily the final production architecture.

Potential improvements:

```text
TimeInForce:
- Day
- GTC
- IOC
- FOK

Cancellation:
- CancelLimitOrder command
- correlate to Saga by OrderId
- update Order = Cancelled
- MarkAsComplete

Amendment:
- change quantity / LimitPrice
- preserve audit history
- correlation/version protection

Expiry:
- timeout at explicit expiry timestamp
- Expired terminal status

Partial fills:
- RemainingQuantity
- multiple execution commands/fills
- Filled only when remaining quantity reaches zero

Trigger robustness:
- avoid duplicate ExecuteLimitOrder on retries
- explicit triggered/execution command IDs
- stronger idempotency across retries
```

A future architecture may replace one-second Saga polling with a more event-driven working-order matcher fed by live market state while keeping the Saga/process manager for durable lifecycle transitions. That should be driven by throughput and latency requirements, not changed prematurely.

---

# 203. Future Trade / Order Audit Improvements

Potential audit fields to consider later:

```text
Order:
SubmittedAt
AcceptedAt
WorkingAt
TriggeredAt
FilledAt
CancelledAt
RejectedAt
LastUpdatedAt

Execution / Trade:
ExecutionId
Venue
LiquidityProvider
ExecutionTimestamp
ReceiveTimestamp
RiskDecisionId
LimitPrice snapshot where useful
```

Do not add all of these now. Add them when a real reporting, execution, compliance, or debugging requirement appears.

---

# 204. Architecture Principle — Prefer Clear Direct Code Over Decorative Indirection

Phase 3B reinforced an important project convention:

Do not introduce tiny helper methods such as:

```text
MarkWorking(...)
MarkTriggered(...)
MarkFilled(...)
```

when they merely hide two obvious state assignments and make the lifecycle harder to read.

Prefer direct code when the business transition is clearer inline:

```text
Data.Status = Triggered
order.Status = Triggered
```

Extract behavior when there is meaningful complexity, reuse, or invariant protection — not simply to reduce line count.

The same principle applies to tests: avoid multiplying near-identical tests when an existing test can naturally carry the relevant assertion or a Theory can represent one business rule cleanly.

---

# 205. Immediate Next Checklist

Before starting Phase 3C:

```text
1. Commit and push the completed Phase 3B milestone.
2. Keep this file as the single cumulative docs/ArchitectureNotes.md.
3. Run the full solution test suite after syncing/commit.
4. Begin Phase 3C with a business discussion of unrealised P&L and mark-price convention.
```

Suggested milestone commit message:

```text
Add working limit order saga execution
```

---

This remains the single cumulative architecture/learning note for the ETrading Platform. Future phases should update this document rather than creating separate phase-specific notes.
