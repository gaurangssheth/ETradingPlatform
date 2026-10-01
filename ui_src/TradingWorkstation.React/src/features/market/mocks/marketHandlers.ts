import { http, HttpResponse } from "msw";
import { mockCandles, mockQuotes } from "./marketMockData";

export const marketHandlers = [
  http.get("*/api/market/quotes", () => {
    return HttpResponse.json(mockQuotes);
  }),
  http.get("*/api/market/candles/:symbol", ({ params }) => {
    const symbol = String(params.symbol);
    return HttpResponse.json(mockCandles(symbol));
  }),
];
