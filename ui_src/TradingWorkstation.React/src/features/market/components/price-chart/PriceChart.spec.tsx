import { mockServer } from "@/mocks/runtime";
import type { HubConnection } from "@microsoft/signalr";
import { screen } from "@testing-library/react";
import { store } from "../../../../store/store";
import { useMarketDataHubConnection } from "../../context/useMarketDataHubConnection";
import { selectSymbol } from "../../state/marketSlice";
import { renderWithProviders } from "../../../../testing/renderWithProviders";
import PriceChart from "./PriceChart";
import type { MarketCandle } from "../../models/MarketCandle";
import { delay, http, HttpResponse } from "msw";

const onMock = vi.fn();
const offMock = vi.fn();

vi.mock("../../context/useMarketDataHubConnection", () => ({
  useMarketDataHubConnection: vi.fn(),
}));

vi.mock("./CandlestickChart", () => ({
  default: ({ candles }: { candles: MarketCandle[] }) => (
    <div data-testid="candlestick-chart">
      <span>{candles.length} candles</span>
      <span data-testid="last-close">{candles.at(-1)?.close}</span>
    </div>
  ),
}));

describe("PriceChart", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    store.dispatch(selectSymbol("EURUSD"));

    vi.mocked(useMarketDataHubConnection).mockReturnValue({
      connection: {
        on: onMock,
        off: offMock,
      } as unknown as HubConnection,
    });
  });

  it("loads candel history for the selected symbol", async () => {
    renderWithProviders(<PriceChart />);

    expect(await screen.findByTestId("candlestick-chart")).toHaveTextContent(
      "2 candles",
    );
  });

  it("subscribes to MarketCandleUpdated", async () => {
    renderWithProviders(<PriceChart />);

    await screen.findByTestId("candlestick-chart");

    const onCall = onMock.mock.calls.find(
      ([eventName]) => eventName === "MarketCandleUpdated",
    );

    expect(onCall).toBeDefined();
  });

  it("adds a live candle when MarketCandleUpdated is received", async () => {
    renderWithProviders(<PriceChart />);

    expect(await screen.findByTestId("candlestick-chart")).toHaveTextContent(
      "2 candles",
    );

    const signalRHandler = onMock.mock.calls.find(
      ([eventName]) => eventName === "MarketCandleUpdated",
    )?.[1];

    expect(signalRHandler).toBeDefined();

    signalRHandler({
      symbol: "EURUSD",
      startTime: "2026-09-30T12:00:10.0000000+00:00",
      open: 1.086,
      high: 1.0865,
      low: 1.0858,
      close: 1.0863,
    });

    expect(await screen.findByTestId("candlestick-chart")).toHaveTextContent(
      "3 candles",
    );
  });

  it("replaces an existing candle when the same bucket is updated", async () => {
    renderWithProviders(<PriceChart />);

    expect(await screen.findByTestId("candlestick-chart")).toHaveTextContent(
      "2 candles",
    );

    const signalRHandler = onMock.mock.calls.find(
      ([eventName]) => eventName === "MarketCandleUpdated",
    )?.[1];

    expect(signalRHandler).toBeDefined();

    signalRHandler({
      symbol: "EURUSD",
      startTime: "2026-09-28T10:30:05.0000000+00:00",
      open: 1.085,
      high: 1.087,
      low: 1.0848,
      close: 1.0868,
    });

    expect(await screen.findByTestId("candlestick-chart")).toHaveTextContent(
      "2 candles",
    );

    expect(await screen.findByTestId("last-close")).toHaveTextContent("1.0868");
  });

  it("preserves a live candle received before REST history completes", async () => {
    mockServer.use(
      http.get("*/api/market/candles/:symbol", async () => {
        await delay(100);

        return HttpResponse.json([
          {
            symbol: "EURUSD",
            startTime: "2026-09-30T12:00:05.0000000+00:00",
            open: 1.085,
            high: 1.0855,
            low: 1.0848,
            close: 1.0851,
          },
        ]);
      }),
    );

    renderWithProviders(<PriceChart />);

    const signalRHandler = onMock.mock.calls.find(
      ([eventName]) => eventName === "MarketCandleUpdated",
    )?.[1];

    expect(signalRHandler).toBeDefined();

    signalRHandler({
      symbol: "EURUSD",
      startTime: "2026-09-30T12:00:05.0000000+00:00",
      open: 1.085,
      high: 1.087,
      low: 1.0848,
      close: 1.0868,
    });

    expect(await screen.findByTestId("candlestick-chart")).toHaveTextContent(
      "1 candles",
    );

    expect(await screen.findByTestId("last-close")).toHaveTextContent("1.0868");
  });

  it("ignores a live candle for a different symbol", async () => {
    renderWithProviders(<PriceChart />);

    expect(await screen.findByTestId("candlestick-chart")).toHaveTextContent(
      "2 candles",
    );

    const signalRHandler = onMock.mock.calls.find(
      ([eventName]) => eventName === "MarketCandleUpdated",
    )?.[1];

    expect(signalRHandler).toBeDefined();

    signalRHandler({
      symbol: "AAPL",
      startTime: "2026-09-30T12:00:10.0000000+00:00",
      open: 250,
      high: 251,
      low: 249,
      close: 250.5,
    });

    expect(await screen.findByTestId("candlestick-chart")).toHaveTextContent(
      "2 candles",
    );
  });

  it("unsubscribes from MarketCandleUpdated when PriceChart is unmounted", async () => {
    const { unmount } = renderWithProviders(<PriceChart />);

    await screen.findByTestId("candlestick-chart");

    const onCall = onMock.mock.calls.find(
      ([eventName]) => eventName === "MarketCandleUpdated",
    );

    expect(onCall).toBeDefined();

    unmount();

    const offCall = offMock.mock.calls.find(
      ([eventName]) => eventName === "MarketCandleUpdated",
    );

    expect(offCall).toBeDefined();
    expect(offCall![1]).toBe(onCall![1]);
  });
});
