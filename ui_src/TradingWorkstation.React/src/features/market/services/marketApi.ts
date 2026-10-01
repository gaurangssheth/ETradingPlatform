import { httpClient } from "../../../shared/http/httpClient";
import type { MarketCandle } from "../models/MarketCandle";
import type { MarketQuote } from "../models/MarketQuote";

export const getMarketQuotes = async () => {
  const response = await httpClient.get<MarketQuote[]>("/api/market/quotes");

  return response.data;
};

export const getMarketCandles = async (
  symbol: string,
): Promise<MarketCandle[]> => {
  const response = await httpClient.get<MarketCandle[]>(
    `/api/market/candles/${symbol}`,
  );

  return response.data;
};
