import { vi } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import MarketWatch from "./MarketWatch";
import type { HubConnection } from "@microsoft/signalr";
import { renderWithProviders } from "../../../../testing/renderWithProviders";
import { store } from "../../../../store/store";
import { selectSymbol } from "../../state/marketSlice";
import { useMarketDataHubConnection } from "../../context/useMarketDataHubConnection";

const onMock = vi.fn();
const offMock = vi.fn();

vi.mock("../../context/useMarketDataHubConnection", () => ({
  useMarketDataHubConnection: vi.fn(),
}));

describe("MarketWatch", () => {
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

  it("updates a quote when a signalR market quote is received", async () => {
    renderWithProviders(<MarketWatch />);

    expect(await screen.findByText("1.0850")).toBeInTheDocument();

    const signalRHandler = onMock.mock.calls.find(
      ([eventName]) => eventName === "MarketQuoteUpdated",
    )?.[1];

    expect(signalRHandler).toBeDefined();

    signalRHandler({
      symbol: "EURUSD",
      bid: 1.086,
      ask: 1.0862,
      spread: 0.0002,
      timestamp: "2026-09-23T10:00:01Z",
    });

    expect(await screen.findByText("1.0860")).toBeInTheDocument();
  });

  it("marks the price as up when the bid increases", async () => {
    renderWithProviders(<MarketWatch />);

    const initialBid = await screen.findByText("1.0850");

    expect(initialBid).toBeInTheDocument();

    const signalRHandler = onMock.mock.calls.find(
      ([eventName]) => eventName === "MarketQuoteUpdated",
    )?.[1];

    signalRHandler({
      symbol: "EURUSD",
      bid: 1.086,
      ask: 1.0862,
      spread: 0.0002,
      timestamp: "2026-09-23T10:00:01.0000000+00:00",
    });

    const updatedBid = await screen.findByText("1.0860");

    expect(updatedBid).toHaveClass("market-watch__price--up");
  });

  it("marks the price as down when the bid decreases", async () => {
    renderWithProviders(<MarketWatch />);

    const initialBid = await screen.findByText("1.0850");

    expect(initialBid).toBeInTheDocument();

    const signalRHandler = onMock.mock.calls.find(
      ([eventName]) => eventName === "MarketQuoteUpdated",
    )?.[1];

    signalRHandler({
      symbol: "EURUSD",
      bid: 1.084,
      ask: 1.0842,
      spread: 0.0002,
      timestamp: "2026-09-23T10:00:01.0000000+00:00",
    });

    const updatedBid = await screen.findByText("1.0840");

    expect(updatedBid).toHaveClass("market-watch__price--down");
  });

  it("marks the price as unchanged when the bid does not change", async () => {
    renderWithProviders(<MarketWatch />);

    expect(await screen.findByText("1.0850")).toBeInTheDocument();

    const signalRHandler = onMock.mock.calls.find(
      ([eventName]) => eventName === "MarketQuoteUpdated",
    )?.[1];

    signalRHandler({
      symbol: "EURUSD",
      bid: 1.085,
      ask: 1.0852,
      spread: 0.0002,
      timestamp: "2026-09-23T10:00:01.0000000+00:00",
    });

    const unchangedBid = await screen.findByText("1.0850");

    expect(unchangedBid).toHaveClass("market-watch__price--unchanged");
  });

  it("selects a symbol when its row is clicked", async () => {
    renderWithProviders(<MarketWatch />);

    const aaplCell = await screen.findByText("AAPL");

    const aaplRow = aaplCell.closest("tr");

    expect(aaplRow).not.toBeNull();

    fireEvent.click(aaplRow!);

    expect(aaplRow).toHaveClass("market-watch__row--selected");
  });

  it("unsubscribes from MarketQuoteUpdated when MarketWatch is unmounted", async () => {
    const { unmount } = renderWithProviders(<MarketWatch />);

    await screen.findByText("EURUSD");

    const onCall = onMock.mock.calls.find(
      ([eventName]) => eventName === "MarketQuoteUpdated",
    );

    unmount();

    const offCall = offMock.mock.calls.find(
      ([eventName]) => eventName === "MarketQuoteUpdated",
    );

    expect(onCall).toBeDefined();
    expect(offCall).toBeDefined();

    expect(offCall![1]).toBe(onCall![1]);
  });
});
