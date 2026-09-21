import { marketHandlers } from "../features/market/mocks/marketHandlers";
import { positionHandlers } from "../features/positions/mocks/positionHandlers";

export const handlers = [...positionHandlers, ...marketHandlers];
