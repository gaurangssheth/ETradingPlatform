import type { MarketCandle } from "../models/MarketCandle";

export const mergeMarketCandle = (
  candles: MarketCandle[],
  updatedCandle: MarketCandle,
): MarketCandle[] => {
  const existingIndex = candles.findIndex(
    (candle) =>
      candle.symbol === updatedCandle.symbol &&
      Date.parse(candle.startTime) === Date.parse(updatedCandle.startTime),
  );

  if (existingIndex >= 0) {
    return candles.map((candle, index) =>
      index === existingIndex ? updatedCandle : candle,
    );
  }

  return [...candles, updatedCandle].sort(
    (a, b) => Date.parse(a.startTime) - Date.parse(b.startTime),
  );
};
