# Trading App Phase 1 Design Pack

This folder is intended to be added to the Visual Studio solution as solution items or kept under a `/docs` folder in the repository.

Phase 1 keeps the project stable and avoids drastic breaking changes.

## Phase 1 Scope

Included:
- TradingGateway.Api as the synchronous entry point
- PricingService.Grpc for price calculation
- RiskService.Grpc for order risk decision
- TradeCapture.Service as a .NET Worker using NServiceBus
- Position.Service as a .NET Worker using NServiceBus
- TradingApp.Contracts for NServiceBus messages
- TradingApp.GrpcContracts for `.proto` contracts
- Shared.Validation reused where useful

Not included yet:
- Angular UI
- React UI
- ZeroMQ market data
- FIX simulator
- Java/Camunda services

## Migration Principle

Do not break the existing solution in one large refactor.

Use this order:

1. Backup/copy the current solution.
2. Rename `Contracts` to `TradingApp.Contracts`.
3. Keep existing `OrderService` compiling first.
4. Rename domain language from e-commerce to trading gradually.
5. Add gRPC contracts.
6. Add PricingService.Grpc.
7. Add RiskService.Grpc.
8. Only then split trade capture and position processing into worker services.

----------
curl -k -X POST "https://localhost:7001/api/orders/submit" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: test-order-001" \
  -d '{
    "clientId": "CLIENT-001",
    "symbol": "EURUSD",
    "side": "Buy",
    "quantity": 1000000,
    "orderType": "Market"
  }'
  
Swagger

{
  "clientId": "CLIENT-001",
  "symbol": "EURUSD",
  "side": "Buy",
  "quantity": 1000000,
  "orderType": "Market"
}

To add migration in OrderService
Add-Migration InitialOrderSchema -Context OrderDbContext -OutputDir Infrastructure\Persistence\Migrations
Update-Database -Project OrderService -StartupProject OrderService -Context OrderDbContext

To add migration TradeCaptureService
Add-Migration InitialTradeSchema -Project TradeCaptureService -StartupProject TradeCaptureService -Context TradeDbContext -OutputDir Infrastructure\Persistence\Migrations
Update-Database -Project TradeCaptureService -StartupProject TradeCaptureService -Context TradeDbContext

To add migration PositionService
Add-Migration InitialPositionSchema -Project PositionService -StartupProject PositionService -Context PositionDbContext -OutputDir Infrastructure\Persistence\Migrations
Update-Database -Project PositionService -StartupProject PositionService -Context PositionDbContext

PRINT 'Stopping services before running this script is recommended.';

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