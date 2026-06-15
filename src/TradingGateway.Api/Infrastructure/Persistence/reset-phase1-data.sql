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