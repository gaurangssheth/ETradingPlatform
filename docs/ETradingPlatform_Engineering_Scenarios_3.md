# E‑Trading Platform — Engineering Scenarios & Interview Notes

> **Purpose**
>
> This document is a running record of engineering situations, design decisions, trade-offs, problems and outcomes from the multi-asset E‑Trading Platform. It is intended for personal bookkeeping, interview preparation, and milestone reviews as the platform grows.
>
> **Interview-use note:** these scenarios describe real technical work and decisions from this platform. They should be presented as work on a multi-asset trading platform without inventing an employer, production incident, team member or client that did not exist. Where a scenario refers to a design discussion, it means the trade-off was deliberately challenged and reviewed before settling on the approach.

---

## Scenario 1 — Replacing RowVersion with a Custom AccountingVersion

**Area / where this happened:** Position Service — concurrency between trade accounting and live mark-to-market updates

In Position Service, the same Position row was being updated for two different reasons. When a real trade arrived, trade accounting changed quantity, average price and realised P&L. Separately, live market-price processing continually updated unrealised P&L for the same position. A conventional SQL `rowversion` would change for every update, so a harmless unrealised-P&L update could make a genuine trade update look stale and cause unnecessary concurrency failures. I challenged whether the concurrency token should represent *any database change* or specifically an *accounting change*. We introduced a custom `AccountingVersion` concurrency token that is incremented by trade accounting but not by MTM. A stale MTM update can therefore be discarded safely while a concurrent trade conflict is still retried. This made the concurrency rule match the business meaning rather than blindly following the database mechanism.

**Key interview points:** optimistic concurrency, EF Core concurrency tokens, lost updates, business-owned versioning, retry versus discard.

---

## Scenario 2 — Treating Stale MTM Differently from a Failed Trade

**Area / where this happened:** Position Service — concurrency recovery for trades versus MTM

Once `AccountingVersion` existed, the next issue was deciding what to do when concurrency actually failed. A trade is an accounting event and must not silently disappear, whereas an MTM update is transient because another market tick will arrive shortly. We therefore allowed trade-side `DbUpdateConcurrencyException` to escape so the message-processing infrastructure can retry it, but handled a stale MTM concurrency exception by logging and discarding that calculation. The next tick recalculates unrealised P&L from the latest persisted position. This was an important discussion because using the same retry policy for both paths would either risk losing trades or waste resources retrying obsolete market data.

**Key interview points:** differentiated recoverability, eventual correction, domain-driven retry policy, transient versus durable business events.

---

## Scenario 3 — Idempotency and Position Accounting in One Transaction

**Area / where this happened:** Position Service — idempotent TradeCaptured processing and accounting transaction

Trade events can be delivered more than once, especially in an event-driven system with retries. The Position Service therefore keeps a `ProcessedTrades` record to detect duplicates. The important design decision was to persist the duplicate marker, position update and position movement through the same Unit of Work/DbContext. If the accounting save fails, the processed marker must not commit independently, otherwise a retry could be incorrectly rejected as a duplicate even though the position was never updated. A unique key protects the duplicate check under concurrency, and the duplicate-key case is handled separately from true accounting concurrency. This made idempotency transactional instead of being only an in-memory check.

**Key interview points:** idempotency, at-least-once delivery, unique constraints, atomicity, Unit of Work, message retries.

---

## Scenario 4 — Moving MTM from Polling to Event-Driven Market Data

**Area / where this happened:** Position Service / Pricing — changing MTM from polling to live market-data events

The first mark-to-market implementation periodically queried all open positions and then called Pricing Service for prices. That proved the calculation path, but it created unnecessary reads and work even when a symbol had not changed. We then replaced the polling path with an event-driven one: ZeroMQ price ticks enter Position Service, a latest-price buffer coalesces updates, and a background worker recalculates only positions affected by changed symbols. This was a deliberate evolutionary decision rather than starting with the most complex design. It allowed correctness to be proved first, then performance and responsiveness to be improved without rewriting the P&L calculators.

**Key interview points:** evolutionary architecture, polling versus push, event-driven processing, reducing redundant I/O.

---

## Scenario 5 — Coalescing Market Ticks Instead of Processing Every Stale Tick

**Area / where this happened:** Position Service — handling bursts of ZeroMQ price ticks

Market data can arrive much faster than position accounting needs to persist updates. Processing every EURUSD tick in order would create a backlog where the system spends time calculating prices that are already obsolete. We introduced a `PriceTickBuffer` backed by a concurrent dictionary containing only the latest tick per symbol, plus a bounded channel used only as a wake-up signal. Newer timestamps replace older ones; workers take the latest unprocessed symbols and calculate from those values. This converts a potentially unbounded stream into “latest state” processing, which is appropriate for MTM where the newest price is what matters.

**Key interview points:** backpressure, coalescing, bounded channels, `ConcurrentDictionary`, latest-value semantics.

---

## Scenario 6 — Parallelising MTM Without Sharing DbContext Across Threads

**Area / where this happened:** Position Service — parallel MTM processing with EF Core

After moving to symbol-driven MTM, independent symbols could be processed concurrently. The temptation was to wrap work in `Task.Run` or share one EF Core `DbContext`, but `DbContext` is not thread-safe and the work is primarily asynchronous database I/O. The worker therefore uses `Task.WhenAll` for different symbols and creates a separate DI scope/DbContext per task. This gives safe concurrency without unnecessary thread-pool scheduling. It also keeps failures isolated to an individual symbol while preserving async I/O all the way down.

**Key interview points:** `Task.WhenAll` versus `Task.Run`, async I/O, DI scopes, EF Core thread safety.

---

## Scenario 7 — Giving Each Transport a Specific Responsibility

**Area / where this happened:** Platform architecture — choosing REST, gRPC, messaging, ZeroMQ and SignalR for different jobs

A recurring architecture discussion was whether one communication technology should be used everywhere. We deliberately separated concerns instead. Browser clients use REST/JSON through `TradingGateway.Api`; internal request/response calls use gRPC; durable business commands/events use NServiceBus with RabbitMQ; high-frequency market ticks use ZeroMQ; and SignalR is reserved for pushing live updates to browser workstations when that UI milestone is reached. The decision was based on communication semantics rather than technology preference. For example, ZeroMQ is useful for fast streaming but is not a replacement for durable trade messaging, while gRPC is efficient internally but is not the browser-facing contract.

**Key interview points:** REST, gRPC, messaging, ZeroMQ, SignalR/WebSockets, choosing transports by semantics.

---

## Scenario 8 — Using gRPC for Internal Service Boundaries

**Area / where this happened:** TradingGateway.Api and internal services — browser REST boundary with internal gRPC

The external workstation needs a stable HTTP/JSON boundary, but the backend services also need strongly typed, efficient service-to-service communication. The Gateway therefore exposes REST to React/Angular and uses generated gRPC clients internally for services such as Position and Pricing. Position Service was converted to the Web SDK so it could continue hosting background workers and NServiceBus handlers while also exposing a gRPC server. This avoided implementation-project references between services and preserved service ownership. Correlation metadata is propagated through the gRPC boundary for traceability.

**Key interview points:** API gateway, protobuf contracts, generated clients, internal versus external APIs, loose coupling.

---

## Scenario 9 — Avoiding Floating-Point Money Values in Protobuf

**Area / where this happened:** Position Service ↔ TradingGateway.Api — transporting decimal accounting values over protobuf

When the position read API was exposed over gRPC, accounting values such as average price and P&L needed to cross the protobuf boundary. Using protobuf `double` would introduce binary floating-point representation into values that are held as `decimal` in .NET. We chose string transport fields for these decimal accounting values and parse/format them using invariant culture on each side. It is slightly more verbose than using `double`, but it preserves the decimal representation expected by the trading/accounting domain and avoids accidental rounding differences at the service boundary.

**Key interview points:** decimal versus double, protobuf limitations, invariant culture, financial precision.

---

## Scenario 10 — Building a CQRS Read Path Without Overengineering

**Area / where this happened:** Position Service and TradingGateway.Api — CQRS read path for open positions

The Gateway originally had a command-oriented path for order submission, but the workstation also needed efficient read endpoints such as open positions by client. We introduced explicit query objects and `IQueryHandler<TQuery,TResult>` / query-dispatcher abstractions rather than mixing reads into command handlers. Position Service maps its domain/entity data into a `PositionSummaryReadModel`, then gRPC transports that to the Gateway, where a REST response model is returned to the browser. The design keeps the read model focused on what the UI needs without turning the whole platform into full event sourcing.

**Key interview points:** CQRS, query handlers, read models, avoiding unnecessary event-sourcing complexity.

---

## Scenario 11 — Implementing Working Limit Orders as a Saga

**Area / where this happened:** Order Service — lifecycle of working limit orders

A market order can execute immediately, but a limit order may remain working until a price condition becomes true. We modelled that as lifecycle/state rather than trying to keep an HTTP request open. Order Service owns the working-order saga, periodically evaluates executable prices, and applies the market rule: a Buy triggers when Ask is less than or equal to the limit; a Sell triggers when Bid is greater than or equal to the limit. Once triggered, the chosen execution price is carried forward so Trade Capture does not reprice it later. This kept order lifecycle ownership in one service while still using Pricing and Trade Capture for their specialised responsibilities.

**Key interview points:** saga/state machine, limit-order rules, asynchronous workflow, preventing repricing races.

---

## Scenario 12 — Keeping Execution Pricing Rules at the Correct Boundary

**Area / where this happened:** Pricing / Order / Trade Capture — deciding and preserving the executable price

The platform distinguishes streamed market data from an executable price. For execution, a Buy trades at the Ask and a Sell trades at the Bid. That rule is deliberately explicit at the pricing/execution boundary rather than being scattered through controllers or UI code. When a working limit order triggers, its already-selected executable price is supplied to Trade Capture so another price lookup cannot change the economic result between trigger and booking. This is a small rule with important architectural consequences because it defines where market data becomes a tradeable decision.

**Key interview points:** bid/ask, executable price, domain rule placement, race avoidance.

---

## Scenario 13 — Strategy Pattern for Multi-Asset Notional and P&L

**Area / where this happened:** Trade Capture and Position accounting — asset-specific notional and P&L calculations

FX, equities and bonds share a trading workflow but do not share identical accounting mathematics. Rather than filling handlers with `switch` statements, the platform uses strategy interfaces and resolvers. FX/equity notional uses quantity multiplied by price, while bond values respect quotation per hundred nominal; realised/unrealised P&L likewise has asset-specific strategies. Each calculator is linked to the `AssetClass` it handles, and the resolver selects the correct implementation. This made the code open to another asset-class calculation without repeatedly modifying a central handler.

**Key interview points:** Strategy pattern, Open/Closed Principle, resolver, asset-class-specific mathematics.

---

## Scenario 14 — Modelling Reference Data for Different Asset Classes

**Area / where this happened:** Reference Data Service — modelling FX, Equity and Bond instruments

A single multi-asset application still needs asset-specific instrument attributes. The Reference Data Service therefore exposes a common instrument definition plus distinct FX, Equity and Bond details. Protobuf `oneof` is used so the transport contract can carry exactly one relevant detail set, while stable instrument IDs allow trades and positions to refer to instruments independently of display symbols. This avoided forcing every asset into one oversized flat structure full of irrelevant nullable fields.

**Key interview points:** polymorphic contracts, protobuf `oneof`, stable identifiers, multi-asset modelling.

---

## Scenario 15 — Building a Generic Validation Framework Instead of Repeating Rules

**Area / where this happened:** Risk / order validation — reusable framework for independent business rules

As order/risk requirements grew, individual handlers risked accumulating duplicated validation logic. We introduced a generic validation approach where individual rules are independently testable and a factory/resolver selects the appropriate validations for the request. This allowed rules such as maximum quantity, supported symbol, market hours, client whitelist and later cross-field validation to be composed without hard-coding every combination into a handler. The framework gave us one consistent way to run validation and report failures, while each business rule remained explicit, independently testable and easy to add.

**Key interview points:** Factory/Strategy-style resolution, composable validation, SRP, cross-field rules, avoiding generic-framework overreach.

---

## Scenario 16 — Using SQLite Carefully in xUnit/EF Core Tests

**Area / where this happened:** Persistence tests — xUnit, EF Core, SQL Server model and SQLite test provider

EF Core tests needed to validate more than in-memory object behaviour, including relationships and database constraints. SQLite was useful because it provides a relational database during tests, but SQL Server and SQLite do not support identical constraint syntax. We added SQLite-specific test configuration where necessary and used `EnsureCreated` in the test setup so tests exercised the relational model without depending on production migrations. This highlighted an important lesson: a lightweight test database is valuable, but provider differences must be explicit so a passing SQLite test is not assumed to prove every SQL Server behaviour.

**Key interview points:** EF Core testing, SQLite provider differences, constraints, `EnsureCreated`, integration-test boundaries.

---

## Scenario 17 — Preserving Correlation IDs Across HTTP, gRPC and Messaging

**Area / where this happened:** Gateway and service boundaries — end-to-end correlation IDs

Once an order crosses multiple services, diagnosing a failure from one log entry is difficult unless the original request identity survives each boundary. The Gateway accepts/creates an HTTP correlation ID, gRPC clients pass it as metadata, and relevant services/logging retain it. We also distinguished request correlation from background processing: an MTM calculation triggered by a new market price should not pretend to belong to an old trade correlation ID. This keeps tracing meaningful instead of simply copying the last available ID everywhere.

**Key interview points:** distributed tracing concepts, metadata propagation, correlation boundaries, observability.

---

## Scenario 18 — Separating Current Position State from Movement Audit

**Area / where this happened:** Position Service — current position state and movement/audit history

A position needs a fast current view, but trading/accounting also requires explaining how that state was reached. The Position Service therefore maintains the current `Position` and separate `PositionMovement` records containing previous/new quantity and price, signed quantity and realised-P&L change. We worked through open, add, reduce, close and flip scenarios rather than assuming one arithmetic formula covered all cases. This design provides both efficient current-state reads and an auditable sequence of accounting movements.

**Key interview points:** audit trail, position accounting, open/add/reduce/close/flip, current state versus history.

---

## Scenario 19 — React Tests in Both jsdom and Real Chromium

**Area / where this happened:** React workstation — Vitest tests in jsdom and real Chromium

For the React workstation I wanted fast tests but also a realistic browser debugging experience. Vitest/jsdom is good for quick unit/component feedback, while Vitest Browser Mode with Playwright/Chromium gives real DOM/browser behaviour and Chrome DevTools debugging. The complication was that MSW has separate Node and browser runtimes: importing `msw/node` into a shared setup caused `node:http` errors in Chromium. We split unit and browser setup files and introduced a small runtime-neutral MSW test controller so the same spec can override handlers whether it runs under jsdom or Chromium. This gave us speed for normal runs without giving up real-browser debugging.

**Key interview points:** Vitest, jsdom, Chromium, Playwright, MSW, environment-specific setup, debugging transformed TypeScript.

---

## Scenario 20 — Making Mock APIs Useful for Development, Not Just Tests

**Area / where this happened:** React workstation — MSW mocks shared by development and tests

The frontend should remain developable even when backend services are not running. Rather than embedding fake arrays inside React components, we use MSW handlers owned by each feature, for example Positions has its own mock data and handlers. A configurable `VITE_API_MODE` allows development to run against the real Gateway or mocked HTTP contracts, while QA/UAT/production are intended to use real backend APIs. The same handlers can also drive tests, so mocked behaviour does not diverge into a second set of test-only fixtures. This preserves the real `positionApi → httpClient` path even when the response is mocked.

**Key interview points:** MSW, contract-oriented frontend development, environment configuration, avoiding hard-coded component mocks.

---

## Scenario 21 — Building a Shared HTTP Layer Rather Than Calling Fetch Everywhere

**Area / where this happened:** React workstation — Axios client, environments and interceptors

As soon as the workstation needed environment-specific URLs, correlation IDs, timeout handling and consistent errors, direct `fetch` calls in components would have duplicated infrastructure concerns. React uses a shared Axios instance configured from Vite environment files for development, QA, UAT and production. Request/response interceptors are kept in separate files so correlation and generic transport errors remain independent concerns. Feature services such as `positionApi` use the shared client, while components deal with business-facing loading/error state. React continues to use `Promise`/`async`/`await` for one-shot HTTP calls rather than introducing RxJS unnecessarily.

**Key interview points:** Axios interceptors, environment configuration, Promise/async-await, separation of transport and feature concerns.

---

## Scenario 22 — Preventing Notification Storms Centrally

**Area / where this happened:** React workstation — avoiding duplicate error notifications

During development, React Strict Mode can deliberately execute effects more than once, and an HTTP error could also be reported both by a generic interceptor and by a feature component. That produced multiple identical red toasts for one logical problem. Instead of disabling Strict Mode or adding flags to individual components, we removed duplicate feature-level transport notifications and gave React-Toastify deterministic `toastId` values based on notification type/message. Duplicate active notifications are therefore suppressed centrally, while the table keeps a persistent inline error state. This solved the user experience problem without hiding useful development behaviour.

**Key interview points:** Strict Mode, centralised cross-cutting concerns, toast deduplication, transient notification versus persistent inline state.

---

## Scenario 23 — Keeping the React Codebase Feature-Oriented as It Grows

**Area / where this happened:** React workstation — feature-based folder structure as the UI grows

A flat `components` and `models` structure was acceptable for the first screen but would become difficult to navigate once Positions, Orders, Trades, Market and Risk all grew. We moved toward feature ownership: each feature contains its components, models, services, state and mocks, while genuinely reusable elements such as navigation, HTTP infrastructure and common table styles live under `shared`. Route-level `pages` compose features rather than owning business logic. This also prepares the application for Redux Toolkit feature slices without creating one giant global store folder.

**Key interview points:** feature-based frontend architecture, cohesion, shared versus feature-owned code, maintainability.

---

## Scenario 24 — Designing the Same Workstation in React and Angular

**Area / where this happened:** Frontend architecture — implementing the same workstation in React and Angular

The workstation is intentionally being built in both React and Angular against the same Gateway contracts. The objective is not to duplicate backend logic but to compare frontend architecture: React uses hooks, Promise-based API calls and later Redux Toolkit feature slices, while Angular can use HttpClient/RxJS, standalone components, Signals/NgRx and newer forms capabilities. Shared business behaviour and API contracts remain identical, allowing differences in state management, testing and rendering to be evaluated fairly. This is also useful for understanding when framework conventions genuinely help rather than simply translating syntax line by line.

**Key interview points:** React versus Angular, framework trade-offs, shared API contract, state management, test strategy.

---

## Scenario 25 — Deliberately Deferring SignalR Until There Was Something Worth Pushing

**Area / where this happened:** Frontend / real-time architecture — deciding when SignalR should be introduced

It would have been easy to introduce SignalR as soon as the UI project was created, but the platform first needed stable read APIs and a meaningful workstation layout. Live market data already flows internally over ZeroMQ; SignalR has a different responsibility — pushing selected server-side changes to browser clients. By deferring SignalR until the REST read path and UI components exist, we avoid building a real-time channel without clear messages, subscription rules or reconnect behaviour. This is an example of resisting premature infrastructure while still preserving a clear future architecture.

**Key interview points:** YAGNI versus roadmap awareness, incremental delivery, SignalR responsibility, avoiding premature complexity.

---

## Scenario 26 — Using Unit of Work to Keep Persistence Behind an Application Boundary

**Area / where this happened:** Position Service — persistence boundary across Positions, ProcessedTrades and PositionMovements

In Position Service, a trade update can touch Positions, ProcessedTrades and PositionMovements as one accounting operation. We introduced an explicit `IUnitOfWork` so the application layer has one place to access those repositories and one `SaveChangesAsync` transaction boundary. Today the implementation uses EF Core, but the application code is not forced to know that every persistence operation must always be EF-based; a repository implementation could later use Dapper or another data-access approach where that makes sense. The main value is a clear persistence boundary and one atomic save for related accounting changes.

**Key interview points:** Unit of Work, repository boundary, EF Core, Dapper, transaction boundary, persistence abstraction.

---

## Scenario 27 — Recoverability Based on gRPC Status Rather Than Retrying Everything

**Area / where this happened:** Service-to-service resilience — deciding which gRPC failures should retry

A distributed service can fail because it is temporarily unavailable, but it can also reject a request permanently because it is invalid or unauthorised. Treating every `RpcException` as transient would cause pointless retries. We therefore explored recoverability rules that distinguish permanent gRPC statuses such as `InvalidArgument`, `PermissionDenied` and `Unauthenticated` from failures that may deserve retry. The broader lesson was to classify errors by semantics rather than by exception type alone.

**Key interview points:** gRPC status codes, transient/permanent failures, retry policy, error queues.

---

## Scenario 28 — Keeping Domain Value Objects and Database Constraints Aligned

**Area / where this happened:** Trade persistence — domain value objects, EF conversions and database constraints

The trade model uses concepts such as currency code, side, order type, status, asset class and stable instrument ID that should not accept arbitrary values. `CurrencyCode` was modelled as a value object and mapped through an EF Core conversion, while database constraints provide a second line of defence for persisted data. Test configuration had to account for SQLite syntax differences. The design balances expressive domain code with database-level integrity rather than assuming validation at one layer is enough.

**Key interview points:** value objects, EF Core conversions, check constraints, defence in depth.

---


## Scenario 29 — Taking Live Market Data All the Way to the Browser Without Polling

**Area / where this happened:** Pricing Service → Trading Gateway → React Market Watch

The backend already had live ZeroMQ price ticks, but the browser still only knew how to ask for a snapshot. Rather than making React poll the Gateway repeatedly, we kept the initial HTTP snapshot and added a separate live path. Pricing Service exposes individual quote updates through a server-streaming gRPC call. A Gateway background worker consumes that internal stream and broadcasts each quote through SignalR to connected workstations. React loads `/api/market/quotes` once for initial state, then listens for `MarketQuoteUpdated` events for subsequent changes. This kept each transport in the role it is good at: ZeroMQ for the incoming transient feed, gRPC streaming between backend services, and SignalR/WebSockets for browser push.

**Key interview points:** server-streaming gRPC, SignalR, WebSockets, snapshot plus delta, async streams, transport boundaries, live trading UI.

---

## Scenario 30 — Resolving a Scoped-Service Lifetime Problem in a Long-Lived Background Worker

**Area / where this happened:** TradingGateway.Api — live market-data bridge

The first Gateway market-stream worker injected `IPricingServiceClient` directly. At runtime ASP.NET Core rejected this because `AddHostedService` creates a singleton hosted service while `IPricingServiceClient` is scoped. Making the pricing client singleton merely to silence DI would have changed its intended lifetime and hidden the design issue. Instead, the worker injects `IServiceScopeFactory`, creates a scope for the streaming session, and resolves the scoped pricing client from that scope. This also prepares the design for reconnect logic, where each new stream attempt can create and dispose its own scope.

**Key interview points:** DI lifetimes, singleton versus scoped, `BackgroundService`, `IServiceScopeFactory`, resource ownership, reconnect design.

---


## Scenario 31 — A Cleanup Test Exposed a Silent SignalR Subscription Leak

**Area / where this happened:** React Market Watch — SignalR lifecycle tests

The live Market Watch registered a SignalR handler with the event name `MarketQuoteUpdated`. The UI appeared to work correctly, but a component-unmount test inspected both the `on(...)` and `off(...)` calls and found that cleanup used `MarketQuoteUpdate` instead. Because the event names did not match, the component stopped the connection but did not unregister the exact handler it had registered. The test compared both the event name and the handler reference, exposing a lifecycle bug that would have been easy to miss through visual testing alone.

The fix was deliberately small: make the cleanup use the exact same event name and callback reference as the subscription.

**Key interview points:** frontend lifecycle, SignalR/WebSocket cleanup, subscription leaks, component unmounting, behavioural testing, catching bugs invisible in the UI.

---

## Scenario 32 — Making Streaming Tests Deterministic Without Inline Continuation Re-entrancy

**Area / where this happened:** Pricing gRPC streaming and Gateway background-worker tests

Testing long-running streams introduced a different problem from ordinary request/response tests: the stream intentionally waits for more data, so the test needs a reliable way to know when a specific event has happened before cancellation. `TaskCompletionSource` was used as an explicit test signal: one task completed when the expected quote was written or broadcast, and another could confirm that the fake long-running stream had observed cancellation.

The test uses `TaskCreationOptions.RunContinuationsAsynchronously` so completing the `TaskCompletionSource` does not force the awaiting continuation to execute inline inside a mock callback. This keeps the test flow easier to reason about and avoids introducing callback re-entrancy or deadlock-style behaviour into the test harness itself. No production deadlock was claimed; the design is a preventative testing choice.

**Key interview points:** async streams, `TaskCompletionSource`, deterministic tests, cancellation, callback re-entrancy, deadlock avoidance, long-running `BackgroundService` testing.

---

# Future Scenarios to Capture as Milestones Are Reached

These are intentionally **not yet written as achievements**. Revisit this document when each milestone is actually implemented and replace the placeholder with the real problem, alternatives considered, decision and outcome.

### Future A — SignalR resilience and scaling
The first live workstation path is now implemented. Future work should capture reconnect/backoff after PricingService restarts, multiple Gateway instances, connection tracking, user/group subscriptions, browser backpressure and any move from JSON to MessagePack if measurements justify it.

### Future B — React Redux Toolkit beyond selected instrument
Redux Toolkit is now in place for shared `market.selectedSymbol`. Capture the next step only when more cross-feature state genuinely needs Redux: orders, selected instrument details, subscriptions or other workstation state. Keep server/loading state local unless sharing it provides a clear benefit.

### Future C — Angular NgRx and Signals
Capture the equivalent Angular design and where Signals, computed state, effects and NgRx each belong. Avoid forcing NgRx into state that is purely local.

### Future D — Angular Signal Forms / modern forms approach
When the order ticket is mature, record how validation, dynamic Buy/Sell/Limit fields and server validation are handled, and compare it with the React form approach.

### Future E — RFC 7807 Problem Details
Introduce a consistent Gateway error contract for validation, business rejection and unexpected errors. Record how React and Angular map it to inline field errors versus global notifications.

### Future F — Authentication and authorisation
Capture identity provider choice, access tokens/cookies, client/trader/risk-manager roles, backend enforcement and why hiding a navigation item is not security.

### Future G — FIX Gateway
Build and capture an inbound FIX session for client/bank-style electronic trading: New Order Single → internal `SubmitOrder`, then outbound ExecutionReport / Reject. Preserve external identifiers such as `ClOrdID` alongside internal order/trade identifiers, and cover session logon, sequence numbers, resend/recovery and idempotency.

### Future H — Multi-liquidity-provider pricing and aggregation
Capture LP connections, stale quote detection, best bid/offer selection, spread/markup rules, venue attribution and whether a dedicated Market Data Service becomes justified.

### Future I — Cloud deployment
Capture the first hosted topology, environment/secrets management, SQL/message broker choices, TLS, observability, cost constraints and what was changed to make locally distributed services cloud-ready.

### Future J — External market-data provider subscription
Capture provider selection, free/demo versus paid feed limitations, throttling/licensing, normalisation into the internal `PriceTick` contract and fallback behaviour.

### Future K — Resilience and observability
Capture structured logging, distributed trace IDs, health/readiness if actually needed, metrics, retry/circuit-breaker decisions and how failures are diagnosed across Gateway → gRPC → messaging.

### Future L — Performance/load testing
Capture measured order throughput, market-tick rate, position-update latency, database bottlenecks and the changes made from evidence rather than assumption.

---


### Future M — Kanban-style delivery and WIP limits
Introduce a lightweight delivery board for the solo build using columns such as Backlog → Ready → In Progress → Test → Done. Keep a small work-in-progress limit, define acceptance criteria for each slice, and record evidence such as passing tests, screenshots, logs or end-to-end runs. Capture whether this improved focus and reduced half-finished cross-cutting work.

### Future N — Distributed multi-laptop environment
Run selected services on separate laptops over the local network instead of copying/running everything on one machine. Capture remote service startup, environment-specific endpoints, firewall/port configuration, service discovery assumptions, logging, failure injection and how restarts/network loss affect gRPC, SignalR, RabbitMQ and SQL dependencies.

### Future O — Workstation layout driven by trading workflow
Rework the desktop workstation so Market Watch selection, selected-instrument chart and Order Ticket sit in one decision flow rather than placing the ticket below Open Positions. Bind the ticket to the selected instrument while still allowing deliberate symbol changes, and record the UX/design trade-offs rather than treating layout as decoration.

# Milestone Review Template

When a meaningful milestone is completed, add a new scenario using this structure:

```text
Scenario N — Short Decision Title

Context / problem:
What business or technical behaviour was required, and what failed or became limiting?

Challenge / discussion:
What assumptions were challenged? What alternatives were considered and what trade-offs mattered?

Decision:
What was chosen and why was it appropriate for this platform at that stage?

Implementation:
Name the important service, framework feature, pattern or algorithm. Keep this short.

Outcome:
What became safer, simpler, faster, more testable or easier to operate? Add measurements when available.

Interview hooks:
Concurrency / distributed systems / trading rule / testing / leadership / mentoring / negotiation / performance / frontend architecture / cloud etc.
```

---

# Interview Framing Guidance

A useful answer does not need to pretend every scenario was a production outage. A strong structure is:

**Situation → technical/business tension → alternatives → decision → implementation → consequence → what I would change at larger scale.**

Examples of legitimate phrasing:

- “While building a multi-asset trading platform, I found that…”
- “The initial design worked functionally, but once MTM and trade accounting ran concurrently…”
- “I challenged whether a database rowversion represented the business concurrency rule we actually needed…”
- “We deliberately kept SignalR out of the first phase because there was not yet a stable browser subscription model…”
- “I first implemented polling to prove correctness, then replaced it with event-driven processing once the behaviour was understood…”

Avoid claiming an employer, production client, team discussion or incident that did not happen. The value in these stories is the engineering reasoning and the working implementation.

---

_Last updated: 2026-09-23. Continue adding scenarios as each meaningful architecture, trading-domain, testing, frontend, delivery or cloud milestone is completed._
