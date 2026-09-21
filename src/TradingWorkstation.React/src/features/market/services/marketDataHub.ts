import {
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
} from "@microsoft/signalr";

const hubUrl = `${import.meta.env.VITE_API_BASE_URL}/hubs/market-data`;

export const createMarketDataHubConnection = (): HubConnection => {
  return new HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();
};
