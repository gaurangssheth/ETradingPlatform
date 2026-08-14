# ETrading Platform — Architecture Notes

**Project:** ETrading Platform  
**Stack:** .NET 8, C#, NServiceBus, RabbitMQ, EF Core, SQL Server, gRPC, Serilog  
**Current state:** Multi-asset order → risk → reference data → pricing → trade capture → position / realised P&L working end-to-end  
**Last updated:** 2026-08-14

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
  ├── PricingService.Grpc
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

> What executable quote is available now?

Current quote model:

```text
Bid
Ask
Mid
```

Current sample prices include:

```text
EURUSD
USDJPY
AAPL
GB00TEST1234
```

Example AAPL:

```text
Mid    210.25
Spread   0.50

Bid    210.00
Ask    210.50
```

Example bond:

```text
Mid    98.45
Spread  0.10

Bid    98.40
Ask    98.50
```

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

Recommended order:

```text
Phase 3A
Live Market Data with ZeroMQ

Phase 3B
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

# 89. Proposed Live Market Data Architecture

Do not replace PricingService gRPC.

Change where PricingService obtains its prices.

Proposed:

```text
MarketDataSimulator
        ↓
ZeroMQ PUB
        ↓
PricingService ZeroMQ SUB
        ↓
Latest Quote Cache
        ↓
existing PricingService gRPC GetQuote
        ↓
TradeCapture
```

This preserves the PricingService boundary.

TradeCapture does not care whether prices came from:

- hardcoded in-memory data
- simulator
- ZeroMQ
- future exchange/vendor
- LP aggregation

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

Initial simulator should publish moving ticks.

Example:

```text
EURUSD
Bid 1.0849
Ask 1.0851

EURUSD
Bid 1.0850
Ask 1.0852

AAPL
Bid 210.00
Ask 210.50

AAPL
Bid 210.25
Ask 210.75

GB00TEST1234
Bid 98.40
Ask 98.50
```

Purpose:

- teach ZeroMQ
- exercise moving prices
- support limit-order testing
- support future unrealised P&L
- avoid immediately depending on a real vendor feed

---

# 112. Initial PriceTick

Likely minimal fields:

```text
Symbol
Bid
Ask
Timestamp
```

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

Do not add future fields until needed.

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

For initial simulator, full recovery is unnecessary.

But sequence concepts are important trading-system interview knowledge.

---

# 114. Market Data Timestamps

Potential future timestamps:

```text
exchange/event timestamp
publisher timestamp
consumer receive timestamp
```

Useful for latency measurement.

Initial simulator only needs a minimal timestamp.

---

# 115. Latest Quote Cache

Proposed PricingService internals:

```text
ZeroMqMarketDataSubscriber
        ↓
PriceTick
        ↓
LatestQuoteStore
        ↓
Pricing gRPC service
```

Potential implementation:

```csharp
ConcurrentDictionary<string, Quote>
```

Do not commit to a concrete abstraction before inspecting the current PricingService code.

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

# 126. Proposed Immediate Next Steps

After committing the current multi-asset Position/P&L work:

## Step 1

Inspect current PricingService structure.

## Step 2

Choose the .NET ZeroMQ library.

## Step 3

Build a tiny learning spike:

```text
Publisher
    ↓
simple message
    ↓
Subscriber prints message
```

Understand each line:

- context/runtime
- socket
- PUB
- SUB
- Bind
- Connect
- topic subscription
- Send
- Receive
- cleanup

## Step 4

Create `MarketDataSimulator`.

## Step 5

Define minimal `PriceTick`.

## Step 6

Add ZeroMQ subscriber background service to PricingService.

## Step 7

Populate latest-quote cache.

## Step 8

Make existing Pricing gRPC service read from the cache.

## Step 9

Add tests for tick ingestion/cache.

## Step 10

Runtime verify moving:

```text
EURUSD
AAPL
GB00TEST1234
```

Then implement Limit Orders.

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

> The platform uses NServiceBus with RabbitMQ for durable business workflow such as order acceptance and trade capture. Live market data is a different workload, so the plan is to use ZeroMQ PUB/SUB for transient high-rate price ticks. A MarketDataSimulator publishes ticks over a ZeroMQ socket, PricingService subscribes in a background component and updates a latest-quote cache, while TradeCapture continues to query the existing PricingService gRPC contract. This keeps the rest of the platform independent of the market-data transport and provides practical experience with bind/connect, sockets, framing, subscriptions, buffering, slow consumers and high-water marks.

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

The following is working:

```text
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

Live bond OPEN and FLIP flows have been successfully verified.

---

# 133. Next Major Milestone

Next major milestone:

```text
Live Market Data
```

Architecture target:

```text
MarketDataSimulator
        ↓ ZeroMQ PUB
PricingService
        ↓ ZeroMQ SUB
LatestQuoteCache
        ↓ existing gRPC
TradeCapture
```

After that:

```text
Working Limit Orders
```

followed by:

```text
Unrealised P&L
SignalR UI
Charts
FIX / external execution
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
= planned fast transient market-data stream
```

This document should remain the single cumulative source of architecture notes as the platform evolves.
