import { useContext } from "react";
import { MarketDataContext } from "./MarketDataContext";

export const useMarketDataHubConnection = () => {
  const context = useContext(MarketDataContext);

  if (!context) {
    throw new Error("useMarketData must be used inside MarketDataProvider");
  }

  return context;
};
