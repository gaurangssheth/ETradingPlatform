import { http, HttpResponse } from "msw";
import { positionMockData } from "./positionMockData";

export const positionHandlers = [
  http.get("*/api/positions", () => {
    return HttpResponse.json(positionMockData);
  }),
];
