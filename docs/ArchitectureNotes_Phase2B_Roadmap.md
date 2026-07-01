# ETrading Platform - Architecture Notes, Phase 2B Scope and Roadmap

Last updated: 2026-07-01

This document captures the current platform direction, the exact scope of Phase 2B, and the roadmap for building the project into a more realistic trading platform. It is intended to be copied into the repository as:

```text
docs/ArchitectureNotes.md
```

Use this as the source of truth when continuing work in a new chat/session.

---

## 1. Platform Goal

The project is an event-driven trading platform built with .NET 8 microservices. The aim is not to build a toy CRUD application, but to gradually evolve toward a realistic trading architecture with:

- Order intake
- Order validation
- Pre-trade risk checks
- Pricing / market data
- Trade capture
- Position accounting
- Realised and unrealised P&L
- Live price and position updates
- Observability and correlation IDs
- Later support for FIX-style external order flow
- Later use of a saga/process manager where the workflow becomes genuinely long-running or multi-step
- Later UI charts/graphs for prices, P&L, exposure and positions

The platform currently uses simplified pricing and execution logic, but the service boundaries are being designed so that later features can be added without redesigning everything.

---

## 2. Current Tool Stack

### Runtime and language

- .NET 8
- C#
- ASP.NET Core Web API
- Worker services

### Messaging and integration

- NServiceBus
- RabbitMQ transport
- SQL persistence / Outbox where configured
- gRPC for internal request/response service calls
- Protocol Buffers `.proto` files for gRPC contracts

### Data access

- EF Core
- SQL Server for service databases
- EF Core migrations
- SQLite in-memory relational database for tests where suitable

### Logging and observability

- Serilog
- Serilog console sink
- Serilog file sink
- Correlation ID middleware
- NServiceBus correlation behaviours
- gRPC metadata correlation

### Testing

- xUnit
- FluentAssertions
- Moq
- NServiceBus.Testing where appropriate
- SQLite in-memory relational DB for EF tests

### Planned future stack

- RiskService.Grpc
- SignalR for browser/UI live updates
- ZeroMQ for backend market-data simulation
- FIX/FIX simulator for external trading-style protocol
- React or Angular UI
- Charting library such as TradingView Lightweight Charts, Highcharts, ECharts, Chart.js, Recharts or similar
- OpenTelemetry later for distributed tracing
- Docker Compose later for local orchestration

---

## 3. Current Services

### TradingGateway.Api

Entry point for external clients and UI.

Current responsibilities:

- Accept HTTP requests
- Validate incoming API commands
- Attach / create correlation ID
- Send `SubmitOrder` command to NServiceBus
- Expose `GET /api/prices/{symbol}` by calling `PricingService.Grpc`
- Return API responses to clients

Important rule:

- The Gateway does not execute trades.
- The Gateway does not decide execution price.

---

### OrderService

Owns order intent and order acceptance.

Current responsibilities:

- Consume `SubmitOrder`
- Persist order intent
- Mark order as accepted
- Publish `OrderAccepted`

Important rule:

- `OrderService` does not own execution price.
- `OrderService.Domain.Order` should not contain execution `Price` or `Notional`.
- `OrderAccepted` should not contain execution `Price` or `Notional`.

Future responsibility:

- Call `RiskService.Grpc` before accepting or rejecting an order.

---

### TradeCaptureService

Owns trade execution/capture.

Current responsibilities:

- Consume `OrderAccepted`
- Call `PricingService.Grpc` for bid/ask/mid
- Choose execution price based on side:
  - Buy uses Ask
  - Sell uses Bid
- Calculate notional
- Persist captured trade
- Publish `TradeCaptured`

Important rule:

- `Trade.Price` is the actual execution price.
- `Trade.Notional` is calculated from execution price and quantity.
- `TradeCaptured` carries `Price` and `Notional`.

---

### PositionService

Owns positions and P&L.

Current responsibilities:

- Consume `TradeCaptured`
- Maintain position quantity and average price
- Maintain realised P&L
- Maintain placeholder unrealised P&L
- Persist position movements/audit trail
- Publish `PositionUpdated`
- Deduplicate processed trades

Important rule:

- Realised P&L is based on actual trade execution prices.
- Unrealised P&L requires live/current market prices and is not fully implemented yet.

---

### PricingService.Grpc

Owns pricing quotes.

Current responsibilities:

- Expose gRPC `GetPrice(symbol)`
- Return bid, ask and mid
- Currently uses in-memory static prices/spreads
- Receives correlation ID through gRPC metadata
- Injects correlation ID into Serilog logs

Important rule:

- Phase 2B PricingService is not full live market data yet.
- It is a service boundary that will later receive live prices from a market data simulator/feed.

---

## 4. Current High-Level Flow

```text
Client / UI
   |
   | HTTP
   v
TradingGateway.Api
   |
   | NServiceBus command: SubmitOrder
   v
OrderService
   |
   | NServiceBus event: OrderAccepted
   v
TradeCaptureService
   |
   | gRPC: GetPrice(symbol)
   v
PricingService.Grpc
   |
   | returns bid/ask/mid
   v
TradeCaptureService
   |
   | NServiceBus event: TradeCaptured
   v
PositionService
   |
   | NServiceBus event: PositionUpdated
   v
Future UI / dashboards
```

---

## 5. Current Technology Boundaries

### HTTP

Used for external API requests:

- Submit order
- Get current price
- Later query orders/trades/positions

### NServiceBus + RabbitMQ

Used for asynchronous business workflow messages:

- `SubmitOrder`
- `OrderAccepted`
- `OrderRejected` later
- `TradeCaptured`
- `PositionUpdated`
- `PriceUpdated` later

NServiceBus/RabbitMQ is good for business events where services should be decoupled and not blocked by each other.

### gRPC

Used for internal synchronous service calls:

- `TradeCaptureService -> PricingService.Grpc`
- `TradingGateway.Api -> PricingService.Grpc`
- Future `OrderService -> RiskService.Grpc`

Use gRPC when one service needs an immediate answer from another service before continuing.

### Serilog

Used for structured logging across services.

### EF Core

Used for service-owned persistence.

Each service owns its own database/schema/context.

---

## 6. What is a `.proto` file?

A `.proto` file is a Protocol Buffers contract file used by gRPC.

It defines:

- The service name
- The RPC methods
- The request message shape
- The response message shape
- Field numbers used for efficient binary serialization

Example from `PricingService.Grpc`:

```proto
syntax = "proto3";

option csharp_namespace = "PricingService.Grpc";

package pricing;

service Pricing {
  rpc GetPrice (GetPriceRequest) returns (GetPriceResponse);
}

message GetPriceRequest {
  string symbol = 1;
}

message GetPriceResponse {
  string symbol = 1;
  double bid = 2;
  double ask = 3;
  double mid = 4;
}
```

This generates C# types such as:

```text
GetPriceRequest
GetPriceResponse
Pricing.PricingBase
Pricing.PricingClient
```

Server side:

```text
PricingGrpcService inherits Pricing.PricingBase
```

Client side:

```text
TradeCaptureService injects Pricing.PricingClient
```

Important rule:

```text
.proto file = service contract
C# generated code = strongly typed client/server classes
```

---

## 7. How gRPC Works in This Platform

### Simple illustration

```text
TradeCaptureService
   |
   | calls generated C# client
   | Pricing.PricingClient.GetPriceAsync(...)
   v
HTTP/2 + Protocol Buffers
   |
   v
PricingService.Grpc
   |
   | PricingGrpcService.GetPrice(...)
   v
GetPriceResponse
```

### Example client call

```csharp
var response = await pricingClient.GetPriceAsync(
    new GetPriceRequest
    {
        Symbol = "EURUSD"
    },
    headers: headers,
    cancellationToken: cancellationToken);
```

### Example server method

```csharp
public override Task<GetPriceResponse> GetPrice(
    GetPriceRequest request,
    ServerCallContext context)
{
    var response = new GetPriceResponse
    {
        Symbol = "EURUSD",
        Bid = 1.0849,
        Ask = 1.0851,
        Mid = 1.0850
    };

    return Task.FromResult(response);
}
```

### Why use gRPC here?

`TradeCaptureService` must know the current price before it can create a trade.

That is a synchronous decision:

```text
No price -> no execution
```

So gRPC is appropriate.

### Why not NServiceBus for this price lookup?

NServiceBus is asynchronous. It is better for workflows/events, not immediate request/response decisions.

A price lookup is more naturally:

```text
Ask now -> get answer now
```

So use gRPC.

---

## 8. Why Interfaces Are Useful in This Project

Interfaces are used to reduce coupling and make code testable.

Examples:

### `IPricingClient`

`TradeCaptureService` depends on:

```csharp
IPricingClient
```

not directly on the generated gRPC client everywhere.

This gives benefits:

- Handler code does not care whether price comes from gRPC, fake client, cache, or another service
- Tests can mock `IPricingClient`
- gRPC-specific metadata logic is isolated in `GrpcPricingClient`
- The handler stays focused on business logic

### `ICorrelatedMessage`

Commands/events implement:

```csharp
ICorrelatedMessage
```

This lets shared NServiceBus behaviours work with all correlated messages in a compile-time safe way.

Instead of reflection like:

```text
Look for a property named CorrelationId dynamically
```

we use:

```csharp
context.Message.Instance is ICorrelatedMessage correlatedMessage
```

Benefits:

- Compile-time safety
- Clear contract
- Easier tests
- No magic reflection
- Future messages can be checked consistently

### Repository / Unit of Work interfaces

Used to keep application/handler logic separate from EF Core persistence details.

Benefits:

- Handlers do not directly manage DbContext details everywhere
- Easier to centralize save/transaction behavior
- Tests can use real EF with SQLite or mock abstractions when appropriate

---

## 9. SOLID and Patterns Used

### Single Responsibility Principle

Each service has one clear business responsibility:

```text
Gateway = API entry point
OrderService = order intent/acceptance
TradeCaptureService = execution/trade capture
PricingService = prices
PositionService = positions/P&L
```

Within services:

```text
PositionCalculator = position math only
ExecutionPriceCalculator = choose bid/ask only
GrpcPricingClient = gRPC pricing integration only
```

### Open/Closed Principle

Using interfaces and service boundaries allows new implementations without changing core handlers too much.

Example:

```text
IPricingClient today -> gRPC implementation
IPricingClient later -> cached/live pricing implementation
```

### Liskov Substitution Principle

Handlers depend on abstractions such as `IPricingClient`, so any valid implementation should be substitutable if it obeys the contract.

### Interface Segregation Principle

Interfaces are small and focused.

Example:

```csharp
IPricingClient.GetPriceAsync(...)
```

It does not contain unrelated methods for orders, positions or risk.

### Dependency Inversion Principle

Higher-level business logic depends on abstractions rather than concrete infrastructure.

Example:

```text
OrderAcceptedHandler -> IPricingClient
not
OrderAcceptedHandler -> raw gRPC channel creation
```

---

## 10. Architectural Patterns Used

### Microservices / service ownership

Each service owns its own responsibility and persistence.

### Event-driven architecture

Business state changes are propagated with NServiceBus events:

```text
OrderAccepted
TradeCaptured
PositionUpdated
```

### Request/response integration

gRPC is used where an immediate answer is needed:

```text
Get current price
Future: check risk
```

### Repository + Unit of Work

Used in services to separate persistence details from handlers.

### Outbox / idempotency

Used to avoid duplicate/unsafe message processing where applicable.

### Pipeline behaviours / middleware

Used for cross-cutting concerns:

- Correlation ID in ASP.NET middleware
- Correlation ID in NServiceBus incoming/outgoing behaviours

### CQRS-style separation

Gateway command handling separates incoming API command validation from backend processing.

The system is not full CQRS/read-model yet, but it is moving in that direction.

### Saga/process manager later

Not introduced yet for the first risk check.

Will be introduced when order workflow becomes long-running/stateful.

---

## 11. Correlation ID Strategy

The platform uses correlation IDs to trace one request/order across multiple services.

### HTTP

External clients send:

```text
X-Correlation-Id: full-flow-001
```

If missing, Gateway middleware creates one.

Important ASP.NET detail:

```csharp
context.Request.Headers.TryGetValue(..., out var value)
```

returns `StringValues`, not `string`.

Always convert using:

```csharp
var correlationId = value.FirstOrDefault();
```

before pushing into Serilog.

Do not push raw `StringValues` into Serilog, otherwise logs may show:

```text
[["price-check-001"]]
```

instead of:

```text
[price-check-001]
```

### Serilog

Serilog output templates contain:

```text
[{CorrelationId}]
```

This placeholder is filled only when code pushes the property:

```csharp
LogContext.PushProperty("CorrelationId", correlationId)
```

### NServiceBus

Commands/events implement:

```csharp
ICorrelatedMessage
```

The outgoing NServiceBus behaviour copies `message.CorrelationId` into message headers:

```text
X-Correlation-Id
```

The incoming NServiceBus behaviour reads the header and pushes it into Serilog `LogContext`.

### gRPC

gRPC does not automatically receive NServiceBus headers. Correlation ID must be sent as metadata:

```text
x-correlation-id
```

This is used by:

- `TradeCaptureService -> PricingService.Grpc`
- `TradingGateway.Api -> PricingService.Grpc`

PricingService reads the metadata and pushes the value into Serilog.

---

## 12. Phase 1 - Completed Scope

Phase 1 proved the distributed flow works.

Covered:

- `TradingGateway.Api`
- `OrderService`
- `TradeCaptureService`
- `PositionService`
- NServiceBus messaging
- RabbitMQ transport
- EF Core persistence
- Basic service databases
- Basic order -> trade -> position flow
- Serilog logging
- Basic correlation ID
- Swagger / API testing
- Idempotency basics

Known simplification in Phase 1:

- Trade price was hardcoded.

---

## 13. Phase 2A - Completed Scope

Phase 2A focused on position accounting and P&L foundation.

Covered:

- `PositionCalculator`
- `PositionCalculationResult`
- Position average price logic
- Position movement/audit table
- Add to position
- Reduce position
- Close position
- Flip position
- Realised P&L
- Placeholder unrealised P&L
- Processed trade deduplication
- Position service tests
- EF relationship tests

Important result:

```text
PositionService can calculate positions from real trade prices.
```

Example:

```text
Buy 100,000 EURUSD @ 1.0851
Sell 50,000 EURUSD @ 1.0849
Remaining position = 50,000
Realised P&L = -10
```

---

## 14. Phase 2B - Current Scope

Phase 2B introduces proper pricing separation and removes execution price from order acceptance.

### Phase 2B goal

```text
TradeCaptureService must no longer use a hardcoded price.
Execution price must come from PricingService.Grpc.
OrderService must remain responsible only for order acceptance, not trade execution.
```

### Covered / being completed

- Add `PricingService.Grpc`
- Add `pricing.proto`
- Add gRPC generated client/server code
- PricingService returns bid/ask/mid
- Buy order executes at Ask
- Sell order executes at Bid
- TradeCaptureService calls PricingService.Grpc
- TradeCaptureService calculates execution notional
- `TradeCaptured` carries execution `Price` and `Notional`
- Gateway exposes `GET /api/prices/{symbol}`
- Correlation ID passed through gRPC metadata
- PricingService injects correlation ID into Serilog
- NServiceBus correlation behaviours added in shared project
- Commands/events implement `ICorrelatedMessage`
- `OrderAccepted` no longer carries execution `Price` or `Notional`
- `OrderService.Domain.Order` no longer stores execution `Price` or `Notional`
- EF migration removes execution price/notional from Orders table
- Tests updated around pricing, correlation and order cleanup

### Phase 2B design rule

```text
Order = client intent/request
Trade = actual execution/fill
```

Therefore:

```text
OrderService.Order.Price      - not allowed
OrderService.Order.Notional   - not allowed
OrderAccepted.Price           - not allowed
OrderAccepted.Notional        - not allowed
Trade.Price                   - allowed
Trade.Notional                - allowed
TradeCaptured.Price           - allowed
TradeCaptured.Notional        - allowed
```

---

## 15. Market Orders vs Limit Orders

### Market order now

For current market orders, the client does not send price.

Client sends:

```json
{
  "clientId": "client-001",
  "symbol": "EURUSD",
  "side": "Buy",
  "quantity": 100000,
  "orderType": "Market"
}
```

TradeCaptureService executes using current quote:

```text
Buy -> Ask
Sell -> Bid
```

### Limit orders later

For limit orders, the client may send a price, but it must be called `LimitPrice`, not `Price`.

Example:

```json
{
  "clientId": "client-001",
  "symbol": "EURUSD",
  "side": "Buy",
  "quantity": 100000,
  "orderType": "Limit",
  "limitPrice": 1.0840
}
```

Meaning:

```text
Buy limit:  buy only if execution price <= limit price
Sell limit: sell only if execution price >= limit price
```

Even then:

```text
LimitPrice = client instruction
Trade.Price = actual execution price
```

---

## 16. Symbols: FX and Stocks

The project currently uses FX symbols such as:

- `EURUSD`
- `GBPUSD`
- `USDJPY`

This does not mean the architecture is limited to FX.

Later it can also support stock symbols such as:

- `AAPL`
- `MSFT`
- `TSLA`
- `BARC.L`
- `HSBA.L`

However, different asset classes introduce different rules.

### FX

- Quantity may represent base currency amount
- Price is quote currency per base currency
- P&L may need currency conversion
- Example: `EURUSD = 1.0850`

### Equities

- Quantity usually means number of shares
- Price is currency per share
- P&L usually in stock trading currency
- Example: `AAPL = 210.50 USD`

Future design may require an `InstrumentService` or `ReferenceDataService` to define:

- Symbol
- Asset class
- Currency
- Tick size
- Lot size
- Trading hours
- Allowed order types

For now, Phase 2B keeps symbols simple strings.

---

## 17. RiskService.Grpc - Next Phase 2C

The next major service should be `RiskService.Grpc`.

### Why RiskService comes next

A realistic trading platform should not blindly accept every order.

Before `OrderService` publishes `OrderAccepted`, it should ask risk:

```text
Can this client place this order?
```

### Phase 2C target flow

```text
SubmitOrder
   -> OrderService
   -> RiskService.Grpc
   -> OrderAccepted or OrderRejected
```

### Initial RiskService checks

Start simple but realistic:

- Client is active
- Symbol is allowed
- Quantity is positive
- Quantity is within max order size
- Notional is within max notional limit
- Client is not blocked
- Basic exposure limit check

### RiskService request example

```text
CheckOrderRiskRequest
- orderId
- clientId
- symbol
- side
- quantity
- orderType
- correlationId
```

Potential future fields:

- limitPrice
- estimatedPrice
- estimatedNotional
- currentPosition
- availableMargin

### RiskService response example

```text
CheckOrderRiskResponse
- approved
- reasonCode
- reason
- riskDecisionId
```

### Order events after RiskService

If approved:

```text
OrderAccepted
```

If rejected:

```text
OrderRejected
```

`OrderRejected` should also implement `ICorrelatedMessage`.

---

## 18. Should RiskService Use a Saga?

For the first RiskService implementation, use synchronous gRPC from `OrderService` to `RiskService.Grpc`.

Reason:

- The initial check is immediate request/response.
- The order should not be accepted until the risk decision is known.
- This keeps the first RiskService phase understandable.

However, because this is intended to become a real system, a saga/process manager should be introduced later when the order lifecycle becomes more complex.

### When a saga becomes useful

Use a saga/process manager when the workflow becomes long-running or multi-step, for example:

```text
OrderSubmitted
   -> RiskCheckRequested
   -> RiskApproved
   -> LiquidityCheckRequested
   -> ExecutionRequested
   -> PartiallyFilled
   -> FullyFilled / Cancelled / Expired / Rejected
```

A saga is useful when the platform needs to track state across multiple asynchronous messages and possible outcomes.

### Future saga candidates

- Order lifecycle saga
- Limit order waiting-for-price saga
- Multi-leg order saga
- Order cancel/amend saga
- Allocation/booking saga
- External venue execution saga

### Important decision

```text
Phase 2C RiskService: gRPC first
Later phase: introduce OrderWorkflowSaga when order lifecycle requires it
```

This avoids over-complicating the current system while still keeping a real-world path open.

---

## 19. Live Price Updates - Future Phase 3A

Current Phase 2B price flow is request/response:

```text
GetPrice(EURUSD) -> returns bid/ask/mid
```

A full platform needs live price updates.

Future target:

```text
MarketDataSimulator
   -> publishes live bid/ask updates
   -> PricingService receives and stores latest price
   -> PricingService publishes PriceUpdated events
   -> Gateway/UI streams updates to user
```

### ZeroMQ role

ZeroMQ is suitable for backend market-data simulation.

Example:

```text
MarketDataSimulator
   -- ZeroMQ PUB/SUB -->
PricingService
```

PricingService would maintain an in-memory/latest-price store:

```text
EURUSD latest bid/ask
AAPL latest bid/ask
MSFT latest bid/ask
```

Then TradeCaptureService continues to call PricingService for current execution price.

ZeroMQ is not for the browser UI. It is backend-to-backend messaging.

---

## 20. UI Live Updates and SignalR - Future Phase 3B

A browser UI needs live updates for:

- Prices
- Orders
- Trades
- Positions
- Realised P&L
- Unrealised P&L
- Risk alerts

For a .NET web UI, SignalR is suitable.

### SignalR role

SignalR should be used for browser-facing real-time updates:

```text
Gateway / UI backend
   -- SignalR/WebSocket -->
Browser UI
```

### Do banks use SignalR?

Banks and trading firms may use WebSockets or similar technologies for web dashboards and internal browser applications.

However, core trading and market-data infrastructure usually uses technologies such as:

- FIX
- Proprietary TCP protocols
- Exchange-native protocols
- Multicast market data
- Kafka
- Aeron
- ZeroMQ
- Vendor market data feeds
- In-memory caches

So in this project:

```text
SignalR = UI/browser updates
ZeroMQ = backend market data simulation
FIX = external order/execution protocol
NServiceBus = business workflow events
gRPC = internal synchronous service calls
```

---

## 21. Graphs and Charts - Future UI Requirement

A trading platform normally needs charts and graphs.

Potential UI features:

### Price charts

- Line chart for price history
- Candlestick chart later
- Bid/ask spread chart
- Symbol comparison chart

### P&L charts

- Realised P&L over time
- Unrealised P&L over time
- Daily P&L chart
- P&L by symbol
- P&L by client/account

### Risk charts

- Exposure by symbol
- Exposure by asset class
- Margin usage
- Limit usage
- Concentration risk

### Position charts

- Position quantity over time
- Long/short exposure
- Top positions

### Implementation options

For web UI:

- React or Angular frontend
- Charting library such as TradingView Lightweight Charts, Highcharts, ECharts, Chart.js, Recharts, or similar
- SignalR for live updates

Backend data needed for graphs:

- Historical prices
- Trades
- Position snapshots
- P&L snapshots
- Risk snapshots

This is not Phase 2B. It belongs after live prices and UI groundwork.

---

## 22. Unrealised P&L - Future Phase 3C

Realised P&L is already handled by trade reductions/closes.

Unrealised P&L needs current market price.

Example:

```text
Long 50,000 EURUSD @ average price 1.0851
Current mid = 1.0860
Unrealised P&L = (1.0860 - 1.0851) * 50,000
```

Future design:

```text
PricingService publishes PriceUpdated
PositionService consumes PriceUpdated
PositionService recalculates UnrealisedPnl
PositionService publishes PositionUpdated
Gateway pushes update to UI through SignalR
```

Important consideration:

- For FX, P&L currency matters.
- For stocks, P&L is usually in the instrument trading currency.
- For multi-currency portfolios, a future FX conversion service may be needed.

---

## 23. FIX - Future Phase 4

FIX is a financial messaging protocol used for orders and executions.

Future service:

```text
FixGatewayService
```

Possible flow:

```text
External FIX client
   -> NewOrderSingle
   -> FixGatewayService
   -> SubmitOrder command
   -> existing platform flow
```

Trade execution back to FIX client:

```text
TradeCaptured
   -> FixGatewayService
   -> ExecutionReport
```

FIX should sit beside the REST Gateway:

```text
REST clients -> TradingGateway.Api
FIX clients  -> FixGatewayService

Both eventually produce SubmitOrder
```

FIX is not part of Phase 2B or Phase 2C.

---

## 24. Suggested Roadmap

### Phase 2B - Finish and commit

Goal:

```text
PricingService and correct trade execution pricing.
```

Final tasks:

- Ensure `OrderService.Domain.Order` has no execution `Price`/`Notional`
- Ensure migration removes `Price`/`Notional` from Orders table
- Ensure tests compile and pass
- Update docs
- Commit Phase 2B

---

### Phase 2C - RiskService.Grpc

Goal:

```text
Pre-trade risk checks before order acceptance.
```

Tasks:

- Add `RiskService.Grpc`
- Add `risk.proto`
- Add `CheckOrderRisk` RPC
- Add request/response contracts
- Add static/in-memory risk rules initially
- `OrderService` calls RiskService before accepting order
- Publish `OrderAccepted` or `OrderRejected`
- Add `OrderRejected` event
- Add tests
- Add correlation ID over gRPC metadata

---

### Phase 2D - Limit orders

Goal:

```text
Support Market and Limit order intent correctly.
```

Tasks:

- Add optional `LimitPrice`
- Market order: `LimitPrice` must be null
- Limit order: `LimitPrice` required
- Add validation rules
- Add pending order state if not executable immediately
- Later integrate with live price updates or saga

---

### Phase 3A - Live market data

Goal:

```text
PricingService receives changing prices.
```

Tasks:

- Add `MarketDataSimulator`
- Add ZeroMQ publisher
- PricingService subscribes to market data
- Maintain latest bid/ask/mid in memory
- Publish `PriceUpdated` events
- Add tests for latest price store

---

### Phase 3B - Real-time UI updates

Goal:

```text
Browser UI receives live updates.
```

Tasks:

- Add SignalR hub
- Stream live prices
- Stream trade updates
- Stream position/P&L updates
- Add React or Angular UI
- Add price grid, trade blotter, position grid
- Add charts/graphs once historical data exists

---

### Phase 3C - Unrealised P&L

Goal:

```text
Positions revalue when market prices change.
```

Tasks:

- PositionService consumes `PriceUpdated`
- Recalculate `UnrealisedPnl`
- Publish updated position/P&L
- Push updates to UI
- Add P&L time-series snapshots

---

### Phase 4 - FIX simulator

Goal:

```text
Support FIX-style external order entry and execution reports.
```

Tasks:

- Add FixGatewayService
- Parse NewOrderSingle
- Map FIX orders to `SubmitOrder`
- Map `TradeCaptured` to ExecutionReport
- Support cancel/amend later

---

### Phase 5 - Production hardening

Goal:

```text
Make the platform more enterprise-like.
```

Tasks:

- Authentication
- Authorization
- API versioning
- Health checks
- OpenTelemetry
- Docker Compose
- CI/CD
- Dead-letter queue monitoring
- Retry policies
- Secrets management
- Load testing
- Observability dashboards

---

## 25. Commit Guidance

When committing Phase 2B, commit the whole solution changes, not only this document.

Before committing:

```powershell
dotnet build
dotnet test
git status
```

Then stage all intended Phase 2B changes:

```powershell
git add .
git status
```

Review the staged files. They may include:

- `PricingService.Grpc`
- `pricing.proto`
- TradeCaptureService gRPC pricing client
- Gateway prices endpoint
- Shared correlation behaviours
- Contract updates such as `ICorrelatedMessage`
- OrderService cleanup and migration
- Tests
- Documentation

Commit message suggestion:

```powershell
git commit -m "feat: add pricing service and execution price flow"
```

Alternative if most code was already committed and this is mostly cleanup/docs:

```powershell
git commit -m "chore: finalise phase 2b pricing and correlation cleanup"
```

---

## 26. Current Best Next Step

After Phase 2B documentation is updated and committed, start:

```text
Phase 2C - RiskService.Grpc
```

Initial RiskService should be gRPC request/response from OrderService.

Do not introduce a saga immediately for the first risk check. Introduce saga later when the order lifecycle becomes multi-step, asynchronous and stateful.

This keeps the system clean:

```text
Now: OrderService -> RiskService.Grpc -> Accept/Reject
Later: OrderWorkflowSaga for complex order lifecycle
```

---

## 27. New Chat Continuation Notes

If work continues in a new chat, use this document as the source of truth.

Important current decisions:

- Phase 2B is about PricingService and execution price separation.
- `Order` is order intent, not execution.
- `Trade` is execution/fill.
- Client does not send execution price for market orders.
- Client may later send `LimitPrice` for limit orders.
- `TradeCaptureService` owns final execution price.
- `PricingService.Grpc` supplies bid/ask/mid.
- Buy uses Ask.
- Sell uses Bid.
- `PositionService` uses trade price for realised P&L.
- Unrealised P&L needs future live/current price updates.
- RiskService.Grpc is next.
- Saga should be introduced later when order workflow becomes genuinely multi-step/stateful.
- SignalR is for browser UI updates.
- ZeroMQ is for backend market data simulation.
- FIX is for external trading protocol simulation.
- Graphs/charts are a future UI requirement after live price/P&L data exists.
- Interfaces are used deliberately for testability and loose coupling.
- `.proto` files are gRPC contracts and generate strongly typed C# client/server code.

Recommended new chat opening message:

```text
Continue ETrading Platform from docs/ArchitectureNotes.md. Phase 2B is completed/being committed. We are starting Phase 2C RiskService.Grpc.
```
