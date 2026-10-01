import type { Position } from "../models/Position";

export const positionMockData: Position[] = [
  {
    instrumentId: "11111111-1111-1111-1111-111111111111",
    symbol: "EURUSD",
    netQuantity: 100000,
    averagePrice: 1.0849,
    pnlCurrency: "USD",
    realisedPnl: 125.5,
    unrealisedPnl: 42.75,
  },
  {
    instrumentId: "22222222-2222-2222-2222-222222222222",
    symbol: "AAPL",
    netQuantity: 50,
    averagePrice: 210.25,
    pnlCurrency: "USD",
    realisedPnl: -35,
    unrealisedPnl: 80.5,
  },
];
