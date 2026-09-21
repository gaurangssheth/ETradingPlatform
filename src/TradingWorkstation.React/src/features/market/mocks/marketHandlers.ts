import { http, HttpResponse } from "msw";
import { marketMockData } from "./marketMockData";

export const marketHandlers = [
  http.get("*/api/market/quotes", () => {
    return HttpResponse.json(marketMockData);
  }),
];
