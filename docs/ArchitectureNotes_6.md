# ETradingPlatform Architecture Notes

_Last updated: 5 September 2026_

## 1. Purpose

ETradingPlatform is a .NET 8 multi-service trading platform used to learn and implement real trading-system concepts through working software.

Preferred learning/design sequence:

**business/trading meaning → market/math rule → architecture/design decision → C# implementation**

Current asset classes:

- FX
- Equity
- Fixed Income

Current order types:

- Market
- Limit

The platform currently covers order submission, risk checks, reference data, executable pricing, trade capture, position accounting, realised P&L, unrealised P&L / mark-to-market, working limit orders, live market data, correlation tracing and optimistic concurrency protection.

---

## 2. Main services

### TradingGateway.Api
HTTP entry point for client requests.

Responsibilities:

- Accept order requests.
- Create/read the `X-Correlation-Id` HTTP header.
- Propagate correlation through the trading workflow.
- Submit commands/messages into the platform.

Important rule: `X-Correlation-Id` belongs in the HTTP header, not in the JSON body.

### OrderService
Owns order lifecycle.

Responsibilities:

- Validate and accept/reject submitted orders.
- Call RiskService.
- Handle Market vs Limit order behaviour.
- Own working Limit Order Saga state.
- Check current executable quote for a working limit order.
- Trigger execution once the market crosses the limit.
- Mark the order Filled when `TradeCaptured` returns.

Market order flow:

```text
SubmitOrder
→ OrderService
→ Risk approved
→ OrderAccepted
→ TradeCaptureService
```

Limit order flow:

```text
SubmitOrder
→ OrderService
→ Risk approved
→ OrderAccepted
→ StartLimitOrder
→ LimitOrderSaga
→ Working
→ quote checks
→ Triggered
→ ExecuteLimitOrder
→ TradeCaptureService
→ TradeCaptured
→ Filled
```

Limit trigger rule:

```text
Buy  → Ask <= LimitPrice
Sell → Bid >= LimitPrice
```

### RiskService
Owns pre-trade risk validation.

Current examples include maximum quantity, allowed symbols and known-client validation.

### ReferenceDataService
Owns stable instrument identity and definitions.

Common fields include InstrumentId, Symbol, AssetClass, TradingCurrency, TickSize, LotSize and IsTradable.

Reference InstrumentIds used during development:

```text
EURUSD       11111111-1111-1111-1111-111111111111
AAPL         22222222-2222-2222-2222-222222222222
GB00TEST1234 33333333-3333-3333-3333-333333333333
```

### PricingService
Owns the latest executable market quote and exposes it through gRPC.

Live market-data path:

```text
MarketDataSimulator
→ ZeroMQ PUB
→ PricingService ZeroMQ SUB
→ MarketQuoteCache
→ PricingGrpcService.GetPrice
```

Important distinction:

```text
PriceTick
= live market-data event
= Symbol, Bid, Ask, Timestamp

PriceQuote
= synchronous gRPC snapshot returned by PricingService
```

Do not rename both concepts to the same thing.

#### MarketQuoteCache
PricingService keeps the latest tick per symbol using `ConcurrentDictionary<string, PriceTick>`.

Newest market timestamp wins:

```csharp
quotes.AddOrUpdate(
    tick.Symbol,
    tick,
    (_, existingTick) =>
        tick.Timestamp > existingTick.Timestamp
            ? tick
            : existingTick);
```

PricingService does not remove quotes after reading them. A current quote remains available until a newer quote replaces it.

### TradeCaptureService
Owns executable trade capture.

Flow:

```text
OrderAccepted / ExecuteLimitOrder
→ ReferenceDataService
→ PricingService when required
→ execution price
→ NotionalCalculatorResolver
→ asset-specific notional calculator
→ persist Trade
→ publish TradeCaptured
```

Market orders: Buy executes at Ask, Sell executes at Bid.

Triggered Limit orders use the supplied execution price and must not be re-priced in TradeCaptureService.

Current notional rules:

```text
FX     = Quantity × Price
Equity = Quantity × Price
Bond   = Quantity × Price / 100
```

For bonds, `Quantity` represents nominal amount and price is quoted per 100 nominal.

Notional currency:

```text
FX          → QuoteCurrency
Equity      → TradingCurrency
FixedIncome → DenominationCurrency
```

### PositionService
Owns current position state, movement audit, realised P&L, unrealised P&L, mark-to-market and trade-accounting concurrency protection.

Position identity:

```text
ClientId + InstrumentId
```

Important fields:

```text
ClientId
InstrumentId
AssetClass
Symbol
NetQuantity
AveragePrice
PnlCurrency
RealisedPnl
UnrealisedPnl
AccountingVersion
CorrelationId
CreatedAt
UpdatedAt
```

`Position.CorrelationId` represents the latest trade correlation ID. Historical trade correlation IDs remain preserved in `PositionMovement`.

---

## 3. Messaging and transport choices

### NServiceBus + RabbitMQ
Used for durable business workflows such as SubmitOrder, OrderAccepted, OrderRejected, TradeCaptured, PositionUpdated and saga commands/timeouts.

### ZeroMQ
Used for transient high-frequency market data.

```text
TradeCaptured
→ durable business event
→ must not be lost

PriceTick
→ transient market-data event
→ freshness is more important than preserving every tick
```

---

## 4. Phase 3A — Live market data

Status: **Complete**

Architecture:

```text
MarketDataSimulator --ZeroMQ PUB--> PricingService
                                         |
                                   MarketQuoteCache
                                         |
                                 gRPC GetPrice
```

Simulator uses shared `TradingApp.MarketData.Contracts.PriceTick`.

Current simulated instruments:

```text
EURUSD: initial bid 1.0849, spread 0.0002, step 0.0001, delay 100–400 ms
AAPL: initial bid 210.00, spread 0.50, step 0.25, delay 250–800 ms
GB00TEST1234: initial bid 98.40, spread 0.10, step 0.05, delay 500–1500 ms
```

Simulator bounded channel:

```text
capacity = 100
FullMode = DropOldest
SingleReader = true
SingleWriter = false
```

`SingleWriter = false` because several instrument simulators write into the same channel.

---

## 5. Phase 3B — Working limit orders

Status: **Complete**

OrderService owns the working-order lifecycle through an NServiceBus Saga.

Important rule:

```text
Order lifecycle state belongs to OrderService.
Execution/trade persistence belongs to TradeCaptureService.
```

---

## 6. Position accounting

`ApplyTrade` handles open, add, reduce, close and flip.

Realised P&L interface:

```csharp
public interface IRealisedPnlCalculator
{
    AssetClass AssetClass { get; }

    decimal Calculate(
        decimal closedQuantity,
        decimal priceDifference);
}
```

Rules:

```text
FX / Equity = closedQuantity × priceDifference
Bond        = closedQuantity × priceDifference / 100
```

`Position.RealisedPnl` is cumulative. `PositionCalculationResult.RealisedPnl` is the current trade effect. `PositionMovement.RealisedPnlChange` records the per-trade change.

---

## 7. Phase 3C — Unrealised P&L / Mark-to-Market

Status: **Event-driven implementation working end-to-end**

Realised P&L is locked in by closing trades. Unrealised P&L is the P&L on an open position using the current close-out price.

Mark-price rule:

```text
Long  → closes by Sell → Bid
Short → closes by Buy  → Ask
```

Formula:

```text
Long  = NetQuantity × (MarkPrice - AveragePrice)
Short = abs(NetQuantity) × (AveragePrice - MarkPrice)
Bond  = same economic calculation / 100
```

Current classes:

```text
IUnrealisedPnlCalculator
FxUnrealisedPnlCalculator
EquityUnrealisedPnlCalculator
BondUnrealisedPnlCalculator
UnrealisedPnlCalculatorResolver
MarkPriceSelector
PositionMarkToMarketCalculator
```

---

## 8. Phase 3C.1 — Polling MTM

Status: **Implemented and runtime-tested, then superseded by 3C.2**

Original architecture:

```text
UnrealisedPnlPositionsBackgroundWorker
→ every ~1 second
→ UnrealisedPnlPositionsUpdater
→ load all open positions
→ group by Symbol
→ one PricingService gRPC request per unique symbol
→ calculate MTM
→ SaveChanges
```

This proved the core business calculation and eventual-consistency behaviour.

Now that 3C.2 is proven, remove the old polling registration and delete `UnrealisedPnlPositionsBackgroundWorker` and `UnrealisedPnlPositionsUpdater` if unused.

Do not remove the core MTM calculators or PricingService gRPC client.

---

## 9. Phase 3C.2 — Event-driven MTM

Status: **Working end-to-end**

Final architecture:

```text
MarketDataSimulator
        |
        | ZeroMQ PriceTick
        v
PositionService ZeroMqPriceTickSubscriber
        |
        v
PriceTickBuffer
        |
        v
UnrealisedPnlPriceTickBackgroundWorker
        |
        v
UnrealisedPnlPriceTickProcessor
        |
        v
PositionRepository
        |
        v
Positions.UnrealisedPnl
```

The old polling worker was disabled during runtime validation and `Positions.UnrealisedPnl` continued moving with live prices. This proved that event-driven `PriceTick` processing was genuinely driving MTM.

---

## 10. PriceTickBuffer

PositionService does not need to process every historical market-data tick.

A burst such as 50 EURUSD ticks should not create 50 SQL valuation updates when only the newest price matters.

`PriceTickBuffer` uses:

```text
ConcurrentDictionary<string, PriceTick>
+ bounded Channel<bool> used only as a wake-up signal
```

The dictionary stores the newest unprocessed tick per symbol. The channel says only: **new work is available**.

With channel capacity 1, many rapid updates can collapse into one pending wake-up.

Newest timestamp wins:

```csharp
latestTicks.AddOrUpdate(
    tick.Symbol,
    tick,
    (_, existingTick) =>
        tick.Timestamp > existingTick.Timestamp
            ? tick
            : existingTick);
```

`TakeLatest()` removes currently available newest ticks and returns them for processing.

`ConcurrentDictionary` enumeration does not freeze other writers. A newer tick that arrives during processing can be inserted concurrently and remain for the next pass.

Intentional behaviour:

```text
process what is available now
+
keep anything newer for the next pass
```

---

## 11. Event-driven MTM background processing

`UnrealisedPnlPriceTickBackgroundWorker`:

1. waits for the PriceTickBuffer wake-up signal
2. calls `TakeLatest()`
3. processes latest ticks
4. creates a separate DI scope / DbContext per tick
5. uses `Task.WhenAll` so different symbols can process concurrently
6. returns to waiting for the next signal

`Task.WhenAll` is preferred over `Parallel.ForEach` because the expensive work is async database I/O (`GetOpenPositionsBySymbolAsync`, `SaveChangesAsync`).

`Task.Run` is still appropriate for the synchronous NetMQ receive loop because that loop blocks synchronously.

---

## 12. ZeroMQ subscriber in PositionService

Responsibility:

```text
ZeroMQ
→ receive topic + payload
→ deserialize PriceTick
→ validate topic matches PriceTick.Symbol
→ PriceTickBuffer.Publish(tick)
```

It performs no EF work, SQL query, P&L calculation or position update. This prevents database latency from blocking market-data receipt.

---

## 13. Optimistic concurrency — AccountingVersion

A SQL Server `rowversion` was considered and rejected because it changes on every row update, including frequent MTM updates, which could cause unnecessary conflicts with business-critical trade accounting.

Position instead has:

```csharp
public long AccountingVersion { get; set; }
```

EF configuration:

```csharp
builder.Property(x => x.AccountingVersion)
    .IsConcurrencyToken();
```

`.IsConcurrencyToken()` does not auto-increment a normal `long`; the application increments it for trade-accounting changes:

```csharp
position.AccountingVersion++;
```

MTM does **not** increment `AccountingVersion`.

Meaning:

> version of the trade-accounting state, not version of every database-row change.

---

## 14. Trade-vs-trade concurrency

Example:

```text
Position = 350k
AccountingVersion = 5

Trade A +50k
Trade B +50k
```

Both may read version 5. Trade A saves 400k/version 6. Trade B's stale update uses `WHERE AccountingVersion = 5`, affects zero rows and causes `DbUpdateConcurrencyException`.

That exception must escape `TradeCapturedHandler` so NServiceBus recoverability retries the message. The retry re-reads the latest state and correctly applies the second trade.

---

## 15. MTM-vs-trade concurrency

If MTM reads accounting version 5 and a trade changes it to 6 before MTM saves, the MTM save is stale and EF throws `DbUpdateConcurrencyException`.

Policy:

```text
stale MTM
→ Debug log
→ discard
→ next PriceTick recalculates using current position
```

Trade concurrency exceptions propagate to NServiceBus; stale MTM concurrency exceptions are benign and can be ignored at Debug level.

---

## 16. ProcessedTrades and idempotency

Current Unit of Work:

```csharp
public interface IUnitOfWork
{
    IPositionRepository Positions { get; }
    IProcessedTradeRepository ProcessedTrades { get; }
    IPositionMovementRepository PositionMovements { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
```

Position, PositionMovement and ProcessedTrade are written through the same Unit of Work / DbContext.

If a trade update fails because of `DbUpdateConcurrencyException`, the ProcessedTrade marker does not commit independently, so an NServiceBus retry can safely process the trade again.

Existing duplicate-key handling remains intentionally narrow:

```csharp
catch (DbUpdateException ex)
    when (IsDuplicateKeyException(ex))
```

A concurrency exception is not swallowed by that filter.

---

## 17. Position repository addition for event-driven MTM

```csharp
Task<IReadOnlyList<Position>>
    GetOpenPositionsBySymbolAsync(
        string symbol,
        CancellationToken cancellationToken = default);
```

Query semantics:

```text
Symbol == requested symbol
AND NetQuantity != 0
```

This allows a market-data tick to update only positions affected by that instrument instead of loading every open position in the platform.

---

## 18. Correlation IDs

Example request:

```text
Header:
X-Correlation-Id: mtm-event-test-001
```

The correlation ID is propagated through business messages.

Background MTM updates must not pretend to originate from an old trade correlation ID. `Position.CorrelationId` remains the correlation of the latest business trade update.

A future enhancement could introduce a separate market-data refresh-cycle or tick trace ID if deeper MTM observability is required.

---

## 19. Logging rules

```text
service lifecycle / meaningful business events → Information
unexpected failures                           → Error
high-frequency market/MTM diagnostics         → Debug or Trace
```

Per-tick successful MTM logging should remain at Trace. Expected stale-MTM concurrency conflicts should be Debug, not Error.

---

## 20. RabbitMQ operational model

```text
Publisher
→ Exchange
→ Binding
→ Queue
→ Consumer
```

Queue metrics:

```text
Ready   = waiting to be delivered
Unacked = delivered but not yet acknowledged
Total   = Ready + Unacked
```

Keep NServiceBus delayed-delivery infrastructure such as `nsb.v2.delay-delivery`. Keep the audit queue while NServiceBus auditing is configured.

---

## 21. Current market-data architecture

```text
                         ┌─────────────────────────────┐
                         │     MarketDataSimulator     │
                         └──────────────┬──────────────┘
                                        │
                                      ZeroMQ
                                        │
                    ┌───────────────────┴───────────────────┐
                    │                                       │
                    v                                       v
          ┌─────────────────────┐              ┌─────────────────────┐
          │   PricingService    │              │   PositionService   │
          │                     │              │                     │
          │ ZeroMQ Subscriber   │              │ ZeroMQ Subscriber   │
          │        ↓            │              │        ↓            │
          │ MarketQuoteCache    │              │ PriceTickBuffer     │
          │        ↓            │              │        ↓            │
          │ gRPC GetPrice       │              │ MTM Worker          │
          └─────────────────────┘              │        ↓            │
                                               │ Position MTM update │
                                               └─────────────────────┘
```

PricingService retains the latest quote for synchronous price requests.

PositionService coalesces the latest unprocessed market-data ticks and uses them to drive event-based valuation.

---

## 22. Current Phase 3C status

Completed:

- Unrealised P&L calculators
- MarkPriceSelector
- PositionMarkToMarketCalculator
- polling MTM implementation and runtime validation
- `AccountingVersion`
- optimistic trade-accounting concurrency test
- event-driven Position repository query
- `PriceTickBuffer`
- ZeroMQ PositionService subscriber
- event-driven MTM processor
- event-driven MTM background worker
- parallel per-symbol processing with separate scopes
- timestamp-based latest-tick selection
- expected stale-MTM concurrency handling
- end-to-end event-driven runtime validation
- live `UnrealisedPnl` confirmed moving while polling worker was disabled

Next cleanup:

- permanently remove old polling worker registration
- delete `UnrealisedPnlPositionsBackgroundWorker`
- delete `UnrealisedPnlPositionsUpdater` if unused
- run full solution build/tests
- commit Phase 3C

---

## 23. Architecture principles learned

### Strategy pattern
Use when there is one business operation, the algorithm differs by category, callers should not own large `if/switch` blocks, and implementations can be selected by a resolver.

Examples:

```text
IRealisedPnlCalculator
IUnrealisedPnlCalculator
NotionalCalculatorResolver
```

Clue: **same business question, different calculation rule depending on asset class.**

### Event vs snapshot
`PriceTick` is an occurrence in a live stream. `PriceQuote` is a current snapshot returned on request.

### Durable vs transient data
Business events need durability. Market ticks favour freshness.

### Eventual consistency
Trade accounting can be persisted immediately and market valuation updated milliseconds later from the next tick.

### Optimistic concurrency
Allow useful concurrency, detect stale writers, and decide retry/discard behaviour according to business importance.

### Coalescing
When later values supersede earlier values, avoid processing obsolete work merely because it entered a FIFO queue first.

---

## 24. Future targets

Potential future phases include:

- FIX connectivity
- real external market-data feeds
- sockets / ZeroMQ deeper learning
- live prices UI
- real-time P&L graphs
- reporting
- Greeks / options risk: Delta, Gamma, Vega, Theta
- rate-options risk
- stress testing
- portfolio-level risk
- authentication / permissions
- tenant isolation
- production monitoring
- deployment and support tooling
- commercial licensing review for dependencies
