# ETradingPlatform Architecture Notes

_Last updated: 1 October 2026_

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

The platform currently covers order submission, risk checks, reference data, executable pricing, trade capture, position accounting, realised P&L, unrealised P&L / mark-to-market, working limit orders, live quote/candle streaming to the browser, selected-instrument candlestick charts, correlation tracing and optimistic concurrency protection.

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

---

## 19. Phase 4 — Gateway read path and browser workstation

Status: **In progress**

The browser boundary is intentionally different from the internal service boundary:

```text
React / Angular
      |
      | REST/JSON
      v
TradingGateway.Api
      |
      | gRPC
      v
internal services
```

Current read path for open positions:

```text
GET /api/positions?clientId=...
→ TradingGateway.Api
→ Gateway query dispatcher
→ GrpcPositionServiceClient
→ PositionService gRPC
→ PositionService query dispatcher
→ GetOpenPositionsByClientIdQueryHandler
→ repository
```

Position accounting decimals are transported through protobuf as invariant-culture strings rather than `double`, then mapped back to `decimal` at the Gateway boundary.

`TradingGateway.Api` also now exposes:

```text
GET /api/market/quotes
```

for an initial market-price snapshot.

The market snapshot path is:

```text
React
→ GET /api/market/quotes
→ TradingGateway.Api
→ GrpcPricingServiceClient
→ PricingService.GetMarketQuotes
→ MarketQuoteCache
```

Gateway HTTP errors are moving toward a standard `ProblemDetails` response contract using:

```csharp
builder.Services.AddProblemDetails();
app.UseExceptionHandler();
```

Custom mappings such as gRPC `Unavailable` → HTTP `503 Service Unavailable` remain a future refinement.

---

## 20. Frontend workstation structure

React and Angular workstations are being built side-by-side against the same Gateway so framework choices can be compared without changing backend business behaviour.

Current React structure is feature-oriented:

```text
src/
├── store/
│   ├── store.ts
│   └── hooks.ts
├── features/
│   ├── positions/
│   └── market/
├── pages/
├── shared/
├── mocks/
└── testing/
```

Current dashboard layout:

```text
┌──────────────┬─────────────────────────────┐
│ Market Watch │ Selected instrument / chart │
├────────────────────────────┬───────────────┤
│ Open Positions             │ Order Ticket  │
├────────────────────────────┴───────────────┤
│ Recent Activity                            │
└────────────────────────────────────────────┘
```

`OpenPositions` uses the real Gateway API path and keeps loading/error states within the table shell.

The React HTTP layer uses a shared Axios client with:

```text
environment-specific base URL
correlation-ID request interceptor
generic transport-error interceptor
feature-specific inline error state
```

MSW is available both for tests and browser development so feature components do not need hard-coded fake arrays.

---

## 21. React testing and debugging

React tests can run in both:

```text
Vitest + jsdom
Vitest browser mode + Playwright Chromium
```

The same component specs can therefore remain fast in normal runs while still being debuggable in a real browser.

MSW has separate Node and browser setup because browser tests must not import `msw/node`.

React DevTools is used for:

```text
component tree
props
hooks/state
render behaviour
Profiler
```

Redux DevTools is used for:

```text
dispatched actions
action payload
state before/after
time-travel inspection
```

---

## 22. Redux workstation state

Redux Toolkit has been introduced for state that is genuinely shared across workstation features.

Current store responsibility is deliberately small:

```text
market.selectedSymbol
```

Example state:

```text
market.selectedSymbol = "EURUSD"
```

Market Watch dispatches `selectSymbol(symbol)`. Other components read the selected value through a typed selector/hook.

Important rule:

> Do not move all server data into Redux merely because Redux exists.

Local request/loading/error state can remain in the owning feature until cross-feature requirements justify moving it.

---

## 23. Error Boundary

A React Error Boundary now protects the application component tree from unexpected rendering/component failures.

This is intentionally separate from API error handling:

```text
HTTP/gRPC/API failure
→ feature error handling / ProblemDetails / notification

unexpected React render failure
→ ErrorBoundary fallback UI
```

The boundary currently logs to the browser console. A future hosted environment can forward these errors to a frontend monitoring/observability service.

---

## 24. Phase 4A — Live Market Watch through gRPC streaming and SignalR

Status: **Working end-to-end**

The browser now receives live market prices without polling.

Architecture:

```text
MarketDataSimulator
      |
      | ZeroMQ PriceTick
      v
PricingService
      |
      | MarketQuoteCache       → snapshot
      |
      | MarketQuoteStream
      v
PricingGrpcService.StreamMarketQuotes
      |
      | server-streaming gRPC
      v
TradingGateway.Api
MarketDataStreamingWorker
      |
      | IHubContext<MarketDataHub>
      v
SignalR
      |
      | normally WebSocket + SignalR Hub Protocol
      v
React MarketWatch
```

Two complementary price paths are retained:

```text
GET /api/market/quotes
→ initial snapshot

SignalR MarketQuoteUpdated
→ subsequent live changes
```

This prevents the live transport from becoming the only way a newly opened workstation can obtain initial state.

### PricingService MarketQuoteStream

`MarketQuoteStream` uses `Channel<PriceTick>` as an asynchronous producer/consumer stream.

```text
PriceTickSubscriberWorker
→ MarketQuoteCache.Update(tick)
→ MarketQuoteStream.Publish(tick)
```

`MarketQuoteCache` answers:

> What is the latest known quote?

`MarketQuoteStream` answers:

> What quote changes are arriving now?

The server-streaming gRPC method consumes the internal async stream with `await foreach` and writes individual `MarketQuote` protobuf messages to the open response stream.

### Gateway streaming client

`GrpcPricingServiceClient.StreamMarketQuotesAsync` exposes the generated gRPC response stream as:

```csharp
IAsyncEnumerable<MarketQuoteResponse>
```

The generated gRPC call object is disposed with `using var call`.

The background worker creates a correlation ID for each long-lived streaming session and propagates it through gRPC metadata. Background work does not reuse an old browser/trade correlation ID.

### Hosted-service lifetime

`AddHostedService<MarketDataStreamingWorker>()` creates a singleton hosted service.

`IPricingServiceClient` is scoped, so it cannot be injected directly into the hosted-service constructor. The worker uses `IServiceScopeFactory` to create an explicit scope and resolve `IPricingServiceClient` from that scope.

Current worker has not yet been given a full reconnect/backoff loop. If PricingService is restarted while Gateway remains up, the gRPC stream can end and prices can stop flowing even though the SignalR browser connection remains alive. This is an intentional next resilience exercise.

### SignalR Hub

`MarketDataHub` logs connect/disconnect events using SignalR `ConnectionId`.

SignalR provides:

```text
Context.ConnectionId
Context.UserIdentifier   (useful later with authentication)
Context.Abort()          (current connection)
```

A user may have multiple SignalR connections, for example several tabs or devices, so user identity and connection identity must not be treated as the same thing.

### React SignalR lifecycle

The React market feature uses the official `@microsoft/signalr` client.

Lifecycle:

```text
component effect
→ connection.on("MarketQuoteUpdated", handler)
→ connection.start()

cleanup
→ connection.off(...)
→ connection.stop()
```

`withAutomaticReconnect()` handles browser-side SignalR reconnect attempts.

React development `StrictMode` can deliberately start/clean/restart effects once, which can produce an initial SignalR negotiation-aborted message during development. Production does not perform that development-only effect check.

---

## 25. Live price movement display

Market Watch currently displays:

```text
Symbol | Bid | Ask | Spread
```

Price updates are merged by symbol. Bid/Ask cells show direction based on the newest quote compared with the immediately previous quote:

```text
new bid > old bid → up
new bid < old bid → down
same bid          → unchanged
```

Direction is kept as:

```ts
type MarketPriceDirection =
  | "up"
  | "down"
  | "unchanged";
```

A timer-based one-second flash was tried and rejected because simulated prices can update much faster than one second. A delayed colour would describe an older price event rather than the newest tick. The current colour therefore follows the latest update.

The simulator currently uses fixed spreads per instrument, so spread normally remains constant while bid/ask move together. Dynamic spreads are a future market-simulation enhancement.

---


## 26. Live market-data test coverage

The live-pricing slice now has meaningful coverage across the backend and React client.

Backend coverage includes:

```text
Pricing gRPC server stream
→ published PriceTick is written as MarketQuote

Gateway gRPC client
→ protobuf MarketQuote maps to MarketQuoteResponse
→ correlation metadata is propagated

MarketDataStreamingWorker
→ streamed quote is broadcast through SignalR
→ cancellation of the long-running test stream is observed
```

`TaskCompletionSource` is used in streaming tests as an explicit synchronization signal so tests wait for the event they care about rather than sleeping for an arbitrary amount of time.

Where appropriate, it is created with:

```csharp
TaskCreationOptions.RunContinuationsAsynchronously
```

so completing the test signal does not force an awaiting continuation to execute inline inside a callback. This keeps the test harness deterministic and avoids introducing callback re-entrancy / deadlock-style behaviour into the test itself.

React Market Watch coverage includes:

```text
initial market snapshot through MSW
SignalR quote update changes displayed price
up / down / unchanged price direction
Redux instrument selection
SignalR cleanup on component unmount
SignalR connection stop on component unmount
```

A cleanup test exposed a real bug:

```text
registered event:   MarketQuoteUpdated
cleanup event:      MarketQuoteUpdate
```

The UI appeared to work, but the wrong cleanup event name meant the exact registered handler was not being removed. The test was strengthened to compare the event name and the handler reference used by `on(...)` and `off(...)`.

---

## 27. Current next milestones

The React workstation remains the reference implementation until the trading workflow and layout stabilise. Angular parity is deliberately moved later so the same design is not rebuilt while it is still changing.

Near-term sequence:

```text
1. Continue React workstation:
   - selected-instrument live candlestick chart is working end-to-end
   - build the functional Order Ticket next to the chart
   - keep Market Watch as instrument chooser
   - Open Positions and Recent Activity below

2. Introduce FIX Gateway:
   - FIX session logon/logout/heartbeat
   - inbound New Order Single (35=D)
   - map to internal SubmitOrder
   - outbound ExecutionReport / Reject (35=8 where applicable)
   - preserve ClOrdID ↔ internal OrderId
   - preserve execution/trade identifiers
   - introduce FIX MsgSeqNum
   - then add sequence-gap / resend handling

3. Increase market-data realism:
   - add ProviderId
   - add market-data SequenceNumber
   - keep source timestamp
   - record receive timestamp
   - calculate delivery latency
   - detect missing / out-of-order ticks
   - run the simulator twice as LP-A and LP-B
   - retain latest quote per provider
   - aggregate best bid / best ask

4. Build distributed multi-laptop environment:
   - Git for code synchronisation
   - SSH / remote terminal for service startup and logs
   - service endpoints configured by machine
   - deliberately introduce service/network failures
   - observe gRPC, SignalR, RabbitMQ and SQL behaviour

5. Introduce lightweight Kanban delivery:
   - Backlog
   - Ready
   - In Progress
   - Test
   - Done
   - keep a small WIP limit
   - acceptance criteria and evidence for each slice

6. Build Angular workstation to feature parity once React behaviour/layout is stable.
```

### FIX Gateway target architecture

```text
FixClientSimulator
       |
       | TCP / FIX session
       v
FIX Gateway
       |
       | NewOrderSingle (35=D)
       | ClOrdID
       v
internal SubmitOrder
       |
       v
existing OrderService / Risk / TradeCapture flow
       |
       v
ExecutionReport / Reject
       |
       | FIX
       v
FixClientSimulator
```

FIX sequence numbers are session-level ordering numbers and are separate from market-data sequence numbers.

```text
FIX MsgSeqNum
= ordering and recovery within a FIX session

Market-data SequenceNumber
= ordering / gap detection within a price feed
```

### Two-liquidity-provider target architecture

The simulator executable should be reusable and launched twice with different configuration rather than creating two different simulator codebases.

```text
MarketDataSimulator
ProviderId = LP-A  ─┐
                    ├─> PricingService
MarketDataSimulator │      |
ProviderId = LP-B  ─┘      ├─ latest quote per provider
                           ├─ sequence / latency checks
                           └─ best bid / best ask aggregation
                                     |
                                     v
                               existing gRPC
                                     |
                                     v
                                  Gateway
                                     |
                                     v
                                  SignalR
                                     |
                                     v
                                workstation
```

Example:

```text
LP-A  Bid 1.0850  Ask 1.0853
LP-B  Bid 1.0851  Ask 1.0854

Best Bid = 1.0851 from LP-B
Best Ask = 1.0853 from LP-A
```

The current three instruments remain sufficient for the first multi-LP and FIX exercises. External/free market-data integration can be introduced later without changing the internal `PriceTick` / quote-processing architecture.

Longer-term:

```text
Gateway gRPC stream reconnect/backoff
external market-data provider
authentication / authorisation
cloud deployment
observability / tracing / metrics
load and performance testing
SignalR scale-out / multi-Gateway fan-out
Angular parity
```

---

## 28. Phase 4B — Live selected-instrument candlestick chart

Status: **Working end-to-end and covered by component tests**

The workstation now combines an initial historical candle snapshot with live candle updates.

```text
initial history:
React PriceChart
→ GET /api/market/candles/{symbol}
→ TradingGateway.Api
→ GrpcPricingServiceClient.GetMarketCandlesAsync
→ PricingService.GetMarketCandles
→ MarketCandleStore

live updates:
MarketDataSimulator
→ ZeroMQ PriceTick
→ PricingService CandleAggregator
→ MarketCandleStream
→ PricingGrpcService.StreamMarketCandles
→ TradingGateway MarketDataStreamingWorker
→ SignalR MarketCandleUpdated
→ React PriceChart
```

`CandleAggregator` builds 5-second OHLC candles from midpoint price:

```text
mid = (Bid + Ask) / 2
```

The candle shape is `Symbol, StartTime, Open, High, Low, Close`. The domain record keeps conventional OHLC ordering, and positional records should be constructed with named arguments.

`MarketCandleStore` keeps recent historical candles while `MarketCandleStream` carries live candle changes. PricingService therefore has the same snapshot-versus-stream distinction used for quotes.

`PricingGrpcService.StreamMarketCandles` consumes the internal async stream with `await foreach`. The Gateway client exposes it as `IAsyncEnumerable<MarketCandleResponse>`. Correlation ID remains gRPC metadata.

`MarketDataStreamingWorker` now runs quote and candle streams together and broadcasts:

```text
MarketQuoteUpdated
MarketCandleUpdated
```

A later resilience exercise should cover one streaming task faulting while the other remains alive.

---

## 29. Shared React market-data connection

The trading page owns one shared SignalR connection through `MarketDataProvider`.

```text
TradingDashboardPage
└── MarketDataProvider
    ├── MarketWatch
    ├── PriceChart
    ├── Order Ticket
    ├── Open Positions
    └── Recent Activity
```

Ownership rule:

```text
MarketDataProvider
→ create/start/stop HubConnection

MarketWatch / PriceChart
→ register/remove event handlers only
```

A real bug was found when the provider's connection-starting `useEffect` had no dependency array. `setConnection(...)` caused another render, which reran cleanup/start. The provider effect now runs once for its component lifetime.

The context remains split into:

```text
MarketDataContext.ts
MarketDataProvider.tsx
useMarketDataHubConnection.ts
```

---

## 30. PriceChart design and candle reconciliation

`PriceChart` owns selected symbol, REST loading, SignalR subscription, candle state, error/loading state and REST/live reconciliation.

`CandlestickChart` owns the Lightweight Charts instance, candlestick series, model-to-chart mapping, visible logical range and external-library cleanup.

Chart/library instances are refs because changing them should not cause React renders.

The chart starts with approximately the latest 25 candles visible. Negative logical positions are valid; clamping to zero when only one or two candles existed made those candles appear excessively wide.

Current EURUSD formatting uses:

```text
precision = 4
minMove = 0.0001
```

Later this should come from ReferenceData instrument metadata.

### `mergeMarketCandle`

Current candle identity:

```text
Symbol + StartTime
```

Rules:

```text
matching bucket → replace
newer/new bucket → append
result → chronological order
```

Timestamp comparison uses the parsed instant rather than raw ISO text because equivalent timestamps can have different fractional precision.

`mergeMarketCandle` is used in two places:

1. Live SignalR updates call it directly against current state.
2. When REST history completes, already-received live candles are reduced over the REST result so a late REST snapshot cannot overwrite fresher live state.

---

## 31. PriceChart test coverage

`PriceChart.spec.tsx` uses the real component/state logic and real `marketApi` HTTP path, with MSW intercepting HTTP. `useMarketDataHubConnection` and the visual `CandlestickChart` child are mocked.

Covered behaviour:

```text
historical candles load for selected symbol
subscribes to MarketCandleUpdated
new live candle appends
same bucket replaces rather than duplicates
live candle wins when REST completes later
different-symbol live candle is ignored
handler is removed on unmount
```

The SignalR `on(...)` method is mocked, but the callback passed into it is the real handler created by `PriceChart`. Tests retrieve that recorded callback and invoke it directly.

---

## 32. Vitest + MSW runtime selection

The same React spec can run in:

```text
unit project    → jsdom
browser project → Playwright / Chromium
```

MSW runtimes differ:

```text
server.ts  → setupServer(...handlers) → msw/node
browser.ts → setupWorker(...handlers) → msw/browser
```

Browser tests must not import `msw/node`, otherwise Vite tries to bundle Node modules such as `node:http`.

Tests import a neutral alias:

```ts
import { mockServer } from "@/mocks/runtime";
```

Vitest resolves it per project:

```text
unit    → runtime.server.ts → server
browser → runtime.browser.ts → browserWorker
```

Temporary handlers must be reset after each test:

```text
server.resetHandlers()
browserWorker.resetHandlers()
```

A missing browser reset caused the delayed one-candle REST handler from one test to leak into the next test.

TypeScript understands `@/*` through `tsconfig.app.json` `paths`; `baseUrl` is not required.

---

## 33. Chart optimisation status

A proposed optimisation changed repeated full `series.setData(...)` calls to selective `series.update(...)`.

The first attempt mixed:
- previous/new data comparison,
- setData versus update decision,
- initial visible-range timing.

When a live candle arrived before REST history, the viewport could initialise against one candle and later historical data appeared squashed or oversized.

Decision:

> Keep the stable implementation now and revisit incremental chart updates as a measured performance exercise.

Future work should compare `setData` versus `update` using React/browser profiling and verify that live updates do not disturb user zoom/scroll.

---

## 34. Production and performance exercises

Production hardening is broader than shutting down services.

Planned deliberate exercises include:

### React / browser
- effect render loops
- duplicate SignalR subscriptions
- stale closures
- unnecessary rerenders
- heavy derived calculations
- `useMemo`, `useCallback`, `React.memo` only after profiling
- symbol switch during an in-flight request
- late REST response overwriting live state
- unbounded candle-array growth
- user scroll/zoom reset by live data
- slow browser consumer

### .NET / concurrency
- sync-over-async
- thread-pool starvation
- ignored cancellation
- shared mutable-state races
- lock/semaphore contention
- one stream faulting while another remains alive

### EF Core / SQL
- optimistic concurrency conflicts
- DbContext concurrent use
- N+1 queries
- tracking overhead
- SQL deadlocks
- idempotency races

### Messaging / market data / FIX
- duplicate, out-of-order and poison messages
- broker reconnect/backlog
- late/duplicate/bad/missing-sequence ticks
- FIX sequence gaps
- duplicate ClOrdID
- PossDup replay
- reconnect/resend recovery

### Infrastructure
- CPU pressure
- memory pressure
- cross-machine latency
- DNS/host faults
- clock skew
- container/service restart

Performance rule:

```text
reproduce
→ measure
→ explain
→ change one thing
→ measure again
```

---

## 35. Order Ticket and next delivery sequence

The current backend supports:

```text
Market
Limit
```

Stop / Stop-Loss and Stop-Limit are not implemented yet.

The first React Order Ticket should therefore use the real current backend contract:

```text
selected Symbol
Buy / Sell
Quantity
Market / Limit
LimitPrice when applicable
Submit
```

Rules:

```text
Market Buy  → Ask
Market Sell → Bid

Limit Buy  → trigger when Ask <= LimitPrice
Limit Sell → trigger when Bid >= LimitPrice
```

Planned later backend extension:

```text
Stop
StopLimit
StopPrice
```

Implement those in OrderService/business workflow first, then expose them in React and FIX.

Current application sequence:

```text
1. Functional React Order Ticket
2. End-to-end workstation order submission
3. Stop / Stop-Limit backend feature
4. FIX counterparty path
5. Instrument-metadata enhancements where UI needs them
```

Infrastructure sequence:

```text
1. One Windows 11 laptop with WSL2
2. Docker Desktop / Linux containers
3. Containerise one service
4. Compose local production-like stack
5. Measure resource usage
6. Add another laptop only when useful
```

Future global log enrichment should add `MachineName`, `ServiceName`, `Environment` and `CorrelationId` centrally.
