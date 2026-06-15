# ETradingPlatform

A Phase 1 event-driven trading platform built with .NET, NServiceBus, RabbitMQ, SQL Server, Entity Framework Core, Serilog, CQRS-style command handling, and microservice-owned databases.

The project models a simplified trading flow:

```text
Client / Swagger / UI
    ↓ HTTP POST /api/orders/submit
TradingGateway.Api
    ↓ SubmitOrder command
Order.Service
    ↓ OrderAccepted event
Tradecapture.Service
    ↓ TradeCaptured event
Position.Service
    ↓ PositionUpdated event
```

---

## 1. Architecture Overview

### Services

| Service | Responsibility |
|---|---|
| `TradingGateway.Api` | External HTTP API. Validates incoming order requests and sends `SubmitOrder` command to NServiceBus. |
| `Order.Service` | Owns order persistence. Handles `SubmitOrder`, saves an order, and publishes `OrderAccepted`. |
| `Tradecapture.Service` | Owns trade capture. Handles `OrderAccepted`, saves a trade, and publishes `TradeCaptured`. |
| `Position.Service` | Owns positions. Handles `TradeCaptured`, updates client/symbol position, records processed trades for idempotency, and publishes `PositionUpdated`. |
| `TradingApp.Contracts` | Shared NServiceBus commands, events, and enums. |
| `TradingApp.Shared` | Shared constants such as endpoint names, connection string names, and correlation header names. |
| `TradingApp.Shared.Validation` | Shared validation framework and reusable validation rules. |

### Infrastructure

| Component | Usage |
|---|---|
| RabbitMQ | Message transport for NServiceBus. |
| SQL Server | Separate service-owned databases. |
| EF Core | Persistence and migrations. |
| NServiceBus Outbox | Used by message handlers to reduce duplicate side effects during retries. |
| NServiceBus Transactional Session | Used by `TradingGateway.Api` to send messages from outside a message handler. |
| Serilog | Structured logging with correlation id. |

---

## 2. Database Ownership

Each service owns its own database:

| Service | Database | Main Tables |
|---|---|---|
| `Order.Service` | `TradingApp_OrderDb` | `Orders` |
| `Tradecapture.Service` | `TradingApp_TradeCaptureDb` | `Trades` |
| `Position.Service` | `TradingApp_PositionDb` | `Positions`, `ProcessedTrades` |

There are no cross-database foreign keys. Relationships are maintained by events and ids such as `OrderId` and `TradeId`.

---

## 3. Message Flow

### Step 1 — Submit Order

`TradingGateway.Api` receives:

```http
POST /api/orders/submit
X-Correlation-Id: phase1-test-001
```

The gateway validates the request. If valid, it sends a `SubmitOrder` command to `Order.Service` and returns HTTP `202 Accepted`.

Important: HTTP `202 Accepted` means the request was accepted for asynchronous processing. It does not mean the order has already been saved, traded, or applied to position.

Gateway response status values:

| Status | Meaning |
|---|---|
| `ValidationFailed` | Input failed gateway validation. No message sent. |
| `Submitted` | Gateway successfully queued the `SubmitOrder` command. |

### Step 2 — Order Accepted

`Order.Service` handles `SubmitOrder`, saves an order, then publishes `OrderAccepted`.

Order status values:

| Status | Meaning |
|---|---|
| `Accepted` | Order was accepted by the order service. |
| `Rejected` | Reserved for future business rejection, not validation failure. |

### Step 3 — Trade Captured

`Tradecapture.Service` handles `OrderAccepted`, saves a trade, then publishes `TradeCaptured`.

`Trades.OrderId` is unique to prevent the same order creating multiple trades.

### Step 4 — Position Updated

`Position.Service` handles `TradeCaptured`, creates or updates one position per:

```text
ClientId + Symbol
```

It also inserts a `ProcessedTrade` row using `TradeId` as the primary key. This protects the position from duplicate `TradeCaptured` messages.

---

## 4. Endpoint Names

Current NServiceBus endpoint names:

```text
TradingGateway.Api
Order.Service
Tradecapture.Service
Position.Service
```

These should be defined centrally in `TradingApp.Shared.Messaging.EndpointNames` and reused by all NServiceBus configuration classes.

---

## 5. Correlation ID

The platform uses the HTTP header:

```text
X-Correlation-Id
```

If the client provides it, the same value is passed through logs and messages. If missing, the gateway creates one.

Expected log pattern:

```text
[12:34:56 INF] [phase1-test-001] TradingGateway.Api... SubmitOrder message sent
[12:34:57 INF] [phase1-test-001] OrderService... Order saved
[12:34:57 INF] [phase1-test-001] TradeCaptureService... Trade captured
[12:34:57 INF] [phase1-test-001] PositionService... Position updated
```

---

## 6. Prerequisites

Install or have available:

- .NET 8 SDK
- SQL Server / SQL Server Developer Edition / SQL Server Express
- RabbitMQ
- Visual Studio 2022 or later
- EF Core tools

EF Core tools command:

```bash
dotnet tool install --global dotnet-ef
```

Or update existing:

```bash
dotnet tool update --global dotnet-ef
```

RabbitMQ Management UI is usually available at:

```text
http://localhost:15672
```

Default local login:

```text
guest / guest
```

---

## 7. Configuration

Each service should have its own connection string in `appsettings.json` or local settings.

Example:

```json
{
  "ConnectionStrings": {
    "GatewayDb": "Server=localhost;Database=TradingApp_GatewayDb;User Id=sa;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True",
    "OrderDb": "Server=localhost;Database=TradingApp_OrderDb;User Id=sa;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True",
    "TradeCaptureDb": "Server=localhost;Database=TradingApp_TradeCaptureDb;User Id=sa;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True",
    "PositionDb": "Server=localhost;Database=TradingApp_PositionDb;User Id=sa;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True"
  },
  "RabbitMQ": {
    "Connection": "host=localhost;username=guest;password=guest"
  }
}
```

Do not commit real local passwords. Use `appsettings.Local.json` for machine-specific secrets and keep it in `.gitignore`.

---

## 8. Running Migrations

### Order Service

```powershell
Add-Migration InitialOrderSchema -Project src\OrderService -StartupProject src\OrderService -Context OrderDbContext -OutputDir Infrastructure\Persistence\Migrations
Update-Database -Project src\OrderService -StartupProject src\OrderService -Context OrderDbContext
```

### Trade Capture Service

```powershell
Add-Migration InitialTradeSchema -Project src\TradeCaptureService -StartupProject src\TradeCaptureService -Context TradeDbContext -OutputDir Infrastructure\Persistence\Migrations
Update-Database -Project src\TradeCaptureService -StartupProject src\TradeCaptureService -Context TradeDbContext
```

### Position Service

```powershell
Add-Migration InitialPositionSchema -Project src\PositionService -StartupProject src\PositionService -Context PositionDbContext -OutputDir Infrastructure\Persistence\Migrations
Update-Database -Project src\PositionService -StartupProject src\PositionService -Context PositionDbContext
```

If using Package Manager Console in Visual Studio and your projects are not under `src\...`, adjust the project names/paths to match your solution.

---

## 9. How to Run

Start RabbitMQ first.

Then run these projects:

```text
TradingGateway.Api
OrderService
TradeCaptureService
PositionService
```

Suggested startup order:

```text
1. OrderService
2. TradeCaptureService
3. PositionService
4. TradingGateway.Api
```

The order is not strict, but subscribers should run at least once so NServiceBus creates event subscriptions.

---

## 10. Test Payloads

### Valid Buy Order

```json
{
  "clientId": "CLIENT-001",
  "symbol": "EURUSD",
  "side": "Buy",
  "quantity": 1000000,
  "orderType": "Market"
}
```

Header:

```text
X-Correlation-Id: phase1-buy-001
```

Expected result:

```text
Order saved
Trade captured
Position created or increased
ProcessedTrades row inserted
```

### Second Buy Same Client/Symbol

```json
{
  "clientId": "CLIENT-001",
  "symbol": "EURUSD",
  "side": "Buy",
  "quantity": 500000,
  "orderType": "Market"
}
```

Expected result:

```text
Same CLIENT-001 + EURUSD position is updated
NetQuantity increases
```

### Sell Same Client/Symbol

```json
{
  "clientId": "CLIENT-001",
  "symbol": "EURUSD",
  "side": "Sell",
  "quantity": 250000,
  "orderType": "Market"
}
```

Expected result:

```text
Same CLIENT-001 + EURUSD position is updated
NetQuantity decreases
```

### Different Symbol

```json
{
  "clientId": "CLIENT-001",
  "symbol": "GBPUSD",
  "side": "Buy",
  "quantity": 250000,
  "orderType": "Market"
}
```

Expected result:

```text
New position row for CLIENT-001 + GBPUSD
```

### Different Client Same Symbol

```json
{
  "clientId": "CLIENT-002",
  "symbol": "EURUSD",
  "side": "Buy",
  "quantity": 300000,
  "orderType": "Market"
}
```

Expected result:

```text
New position row for CLIENT-002 + EURUSD
```

### Invalid Order

```json
{
  "clientId": "CLIENT-001",
  "symbol": "EURUSD",
  "side": "WrongSide",
  "quantity": 1000000,
  "orderType": "Market"
}
```

Expected gateway response:

```text
Status = ValidationFailed
No SubmitOrder message sent
No order row created
```

---

## 11. Useful SQL Checks

### Orders

```sql
SELECT TOP 20 *
FROM TradingApp_OrderDb.dbo.Orders
ORDER BY CreatedAt DESC;
```

### Trades

```sql
SELECT TOP 20 *
FROM TradingApp_TradeCaptureDb.dbo.Trades
ORDER BY CapturedAt DESC;
```

### Positions

```sql
SELECT TOP 20 *
FROM TradingApp_PositionDb.dbo.Positions
ORDER BY UpdatedAt DESC;
```

### Processed Trades

```sql
SELECT TOP 20 *
FROM TradingApp_PositionDb.dbo.ProcessedTrades
ORDER BY ProcessedAt DESC;
```

### Check for Duplicate Processed Trades

```sql
SELECT TradeId, COUNT(*) AS CountPerTrade
FROM TradingApp_PositionDb.dbo.ProcessedTrades
GROUP BY TradeId
HAVING COUNT(*) > 1;
```

Expected: no rows.

---

## 12. Reset Script

Stop all services before running this script.

```sql
PRINT 'Stop all services before running this script.';

USE TradingApp_PositionDb;
PRINT 'Clearing PositionService data...';
DELETE FROM dbo.ProcessedTrades;
DELETE FROM dbo.Positions;

USE TradingApp_TradeCaptureDb;
PRINT 'Clearing TradeCaptureService data...';
DELETE FROM dbo.Trades;

USE TradingApp_OrderDb;
PRINT 'Clearing OrderService data...';
DELETE FROM dbo.Orders;

PRINT 'Phase 1 test data cleared.';
```

Do not delete NServiceBus infrastructure tables during normal testing. These may include subscription, timeout, outbox, saga, audit, or endpoint-specific tables.

If RabbitMQ queues contain old test messages, purge only development queues from RabbitMQ Management UI after stopping services.

---

## 13. Idempotency

### Trade Capture Idempotency

`Tradecapture.Service` protects against duplicate order processing by making `Trades.OrderId` unique.

### Position Idempotency

`Position.Service` protects against duplicate `TradeCaptured` events using `ProcessedTrades`.

Flow:

```text
TradeCaptured received
    ↓
Check ProcessedTrades by TradeId
    ↓
If exists: skip
    ↓
If not exists: update position and insert ProcessedTrade
    ↓
SaveChanges
```

The database primary key on `ProcessedTrades.TradeId` is the final guarantee. The code also catches duplicate key exceptions to handle race conditions where duplicate messages arrive at nearly the same time.

---

## 14. Realised and Unrealised P&L — Future Work

The current Phase 1 position logic tracks:

```text
NetQuantity
AveragePrice
```

Real systems usually also track:

```text
RealisedPnL
UnrealisedPnL
MarkToMarketPrice
LastPriceUpdatedAt
```

### Realised P&L

Profit or loss from the part of the position that has been closed.

Example:

```text
Buy 1,000,000 EURUSD @ 1.0850
Sell 400,000 EURUSD @ 1.0900
Realised P&L = (1.0900 - 1.0850) * 400,000 = 2,000
```

### Unrealised P&L

Profit or loss on the currently open position using current market price.

Example:

```text
Long 600,000 EURUSD @ 1.0850
Current price = 1.0910
Unrealised P&L = (1.0910 - 1.0850) * 600,000 = 3,600
```

Future flow:

```text
TradeCaptured → PositionService updates quantity, average price, realised P&L
PriceUpdated  → PositionService or RiskService updates unrealised P&L
```

---

## 15. Phase 1 Completion Checklist

- [x] Gateway accepts order submission.
- [x] Gateway validates request.
- [x] Gateway sends `SubmitOrder` using NServiceBus transactional session.
- [x] Order service saves order.
- [x] Order service publishes `OrderAccepted`.
- [x] Trade capture service saves trade.
- [x] Trade capture service publishes `TradeCaptured`.
- [x] Position service updates position.
- [x] Position service records processed trades for idempotency.
- [x] Correlation id flows through logs and messages.
- [x] SQL reset script exists.
- [ ] Clean old template code from previous Order/Billing/Shipping examples.
- [ ] Add automated integration tests.
- [ ] Add GitHub Actions build pipeline.

---

## 16. Next Phases

Potential next development phases:

```text
Phase 2: Better position accounting with realised/unrealised P&L
Phase 3: PricingService.Grpc for current market prices
Phase 4: RiskService.Grpc for pre-trade risk checks
Phase 5: SignalR live order/trade/position updates
Phase 6: Angular UI
Phase 7: React UI
Phase 8: ZeroMQ market data simulator
Phase 9: FIX simulator
```
