import type { MarketQuote } from "../models/MarketQuote";

export const marketMockData: MarketQuote[] = [
  {
    symbol: "AAPL",
    bid: 208.25,
    ask: 208.75,
    spread: 0.5,
    timestamp: "2026-09-19T07:00:00.0000000+00:00",
  },
  {
    symbol: "EURUSD",
    bid: 1.085,
    ask: 1.0852,
    spread: 0.0002,
    timestamp: "2026-09-19T07:00:00.0000000+00:00",
  },
  {
    symbol: "GB00TEST1234",
    bid: 98.2,
    ask: 98.3,
    spread: 0.1,
    timestamp: "2026-09-19T07:00:00.0000000+00:00",
  },
];
