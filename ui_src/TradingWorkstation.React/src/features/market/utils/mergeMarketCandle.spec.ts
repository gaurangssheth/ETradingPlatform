import type { MarketCandle } from "../models/MarketCandle";
import { mergeMarketCandle } from "./mergeMarketCandle";

describe("mergeMarketCandle", () => {
  it("replaces an existing candle with the same symbol and start time", () => {
    const existing: MarketCandle = {
      symbol: "EURUSD",
      startTime: "2026-09-28T10:30:00Z",
      open: 1.085,
      high: 1.0852,
      low: 1.0849,
      close: 1.0851,
    };

    const updated: MarketCandle = {
      ...existing,
      high: 1.0857,
      close: 1.0856,
    };

    const result = mergeMarketCandle([existing], updated);

    expect(result).toHaveLength(1);
    expect(result[0]).toEqual(updated);
  });

  it("appends a new candle in chronological order", () => {
    const existing: MarketCandle = {
      symbol: "EURUSD",
      startTime: "2026-09-28T10:30:10Z",
      open: 1.085,
      high: 1.0852,
      low: 1.0849,
      close: 1.0851,
    };

    const updated: MarketCandle = {
      ...existing,
      startTime: "2026-09-28T10:30:05Z",
      close: 1.0853,
    };

    const result = mergeMarketCandle([existing], updated);

    expect(result).toHaveLength(2);
    expect(result.map((candle) => candle.startTime)).toEqual([
      "2026-09-28T10:30:05Z",
      "2026-09-28T10:30:10Z",
    ]);
  });

  it("does not replace a candle belonging to another symbol", () => {
    const existing: MarketCandle = {
      symbol: "EURUSD",
      startTime: "2026-09-28T10:30:00Z",
      open: 1.085,
      high: 1.0852,
      low: 1.0849,
      close: 1.0851,
    };

    const updated: MarketCandle = {
      ...existing,
      symbol: "GBPUSD",
      close: 1.35,
    };

    const result = mergeMarketCandle([existing], updated);

    expect(result).toHaveLength(2);
    expect(result).toContainEqual(existing);
    expect(result).toContainEqual(updated);
  });
});
