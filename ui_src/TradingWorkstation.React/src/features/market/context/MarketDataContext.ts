import type { HubConnection } from "@microsoft/signalr";
import { createContext } from "react";

export type MarketDataContextValue = {
  connection: HubConnection | null;
};

export const MarketDataContext = createContext<
  MarketDataContextValue | undefined
>(undefined);
