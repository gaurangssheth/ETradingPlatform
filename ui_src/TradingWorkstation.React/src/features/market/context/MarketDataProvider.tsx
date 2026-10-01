import { HubConnection } from "@microsoft/signalr";
import { useEffect, useState, type ReactNode } from "react";
import { createMarketDataHubConnection } from "../services/marketDataHub";
import { MarketDataContext } from "./MarketDataContext";

type MarketDataProviderProps = {
  children: ReactNode;
};

export const MarketDataProvider = ({ children }: MarketDataProviderProps) => {
  const [connection, setConnection] = useState<HubConnection | null>(null);

  useEffect(() => {
    const hubConnection = createMarketDataHubConnection();

    let disposed = false;

    const startConnection = async () => {
      try {
        await hubConnection.start();
        if (!disposed) {
          setConnection(hubConnection);
        }
      } catch (error) {
        if (!disposed) {
          console.error(
            "Failed to start SignalR market data connection.",
            error,
          );
        }
      }
    };

    startConnection();

    return () => {
      disposed = true;
      void hubConnection.stop();
    };
  }, []);

  return (
    <MarketDataContext.Provider value={{ connection }}>
      {children}
    </MarketDataContext.Provider>
  );
};
