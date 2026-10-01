import type { MarketCandle } from "../models/MarketCandle";
import type { MarketQuote } from "../models/MarketQuote";

export const mockQuotes: MarketQuote[] = [
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

export const mockCandles = (symbol: string): MarketCandle[] => [
  {
    symbol,
    startTime: "2026-09-28T10:30:00.0000000+00:00",
    open: 1.085,
    high: 1.0855,
    low: 1.0848,
    close: 1.0852,
  },
  {
    symbol,
    startTime: "2026-09-28T10:30:05.0000000+00:00",
    open: 1.0852,
    high: 1.0858,
    low: 1.0851,
    close: 1.0856,
  },
];
