import { httpClient } from "../../../shared/http/httpClient";
import type { MarketQuote } from "../models/MarketQuote";

export const getMarketQuotes = async () => {
  const response = await httpClient.get<MarketQuote[]>("/api/market/quotes");

  return response.data;
};
