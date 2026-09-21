import { useEffect, useState } from "react";
import type { MarketQuote } from "../models/MarketQuote";
import { getMarketQuotes } from "../services/marketApi";
import { formatMarketPrice } from "../utils/formatMarketPrice";
import { useAppSelector, userAppDisptch } from "../../../store/hooks";
import { selectedSymbolSelector, selectSymbol } from "../state/marketSlice";
import { createMarketDataHubConnection } from "../services/marketDataHub";
import type { MarketPriceDirection } from "../models/MarketPriceDirection";
import "./MarketWatch.scss";
import "./MarketWatchSkeleton.scss";

export const MarketWatch = () => {
  const [quotes, setQuotes] = useState<MarketQuote[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [priceDirections, setPriceDirections] = useState<
    Record<string, MarketPriceDirection>
  >({});

  const dispatch = userAppDisptch();
  const selectedSymbol = useAppSelector(selectedSymbolSelector);

  useEffect(() => {
    const loadMarketQuotes = async () => {
      try {
        const result = await getMarketQuotes();
        setQuotes(result);
        setError(null);
      } catch {
        setError("Unable to load market quotes.");
      } finally {
        setIsLoading(false);
      }
    };
    loadMarketQuotes();
  }, []);

  useEffect(() => {
    const connection = createMarketDataHubConnection();

    let disposed = false;

    const handleMarketQuoteUpdated = (updatedQuote: MarketQuote) => {
      setQuotes((currentQuotes) => {
        const existingQuote = currentQuotes.find(
          (quote) => quote.symbol === updatedQuote.symbol,
        );

        if (existingQuote) {
          let direction: MarketPriceDirection = "unchanged";
          if (updatedQuote.bid > existingQuote.bid) {
            direction = "up";
          } else if (updatedQuote.bid < existingQuote.bid) {
            direction = "down";
          }

          setPriceDirections((current) => ({
            ...current,
            [updatedQuote.symbol]: direction,
          }));
        }

        return currentQuotes.map((quote) =>
          quote.symbol === updatedQuote.symbol ? updatedQuote : quote,
        );
      });
    };

    connection.on("MarketQuoteUpdated", handleMarketQuoteUpdated);

    const startConnection = async () => {
      try {
        await connection.start();
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

      connection.off("MarketQuoteUpdate", handleMarketQuoteUpdated);

      void connection.stop();
    };
  }, []);

  const renderLoadingRows = () => (
    <>
      {Array.from({ length: 3 }).map((_, index) => (
        <tr key={index}>
          <td className="symbol-column">
            <span className="market-watch-skeleton__cell" />
          </td>

          <td className="numeric-column">
            <span className="market-watch-skeleton__cell" />
          </td>

          <td className="numeric-column">
            <span className="market-watch-skeleton__cell" />
          </td>

          <td className="numeric-column">
            <span className="market-watch-skeleton__cell" />
          </td>
        </tr>
      ))}
    </>
  );

  const renderError = () => (
    <tr>
      <td className="market-watch__message" colSpan={4}>
        {error}
      </td>
    </tr>
  );

  const renderMarketQuotes = () =>
    quotes.map((quote) => (
      <tr
        key={quote.symbol}
        className={
          quote.symbol === selectedSymbol
            ? "market-watch__row market-watch__row--selected"
            : "market-watch__row"
        }
        onClick={() => dispatch(selectSymbol(quote.symbol))}
      >
        <td className="symbol-column">{quote.symbol}</td>

        <td
          className={`numeric-column market-watch__price market-watch__price--${
            priceDirections[quote.symbol] ?? "unchanged"
          }`}
        >
          {formatMarketPrice(quote.symbol, quote.bid)}
        </td>

        <td
          className={`numeric-column market-watch__price market-watch__price--${
            priceDirections[quote.symbol] ?? "unchanged"
          }`}
        >
          {formatMarketPrice(quote.symbol, quote.ask)}
        </td>

        <td className="numeric-column">
          {formatMarketPrice(quote.symbol, quote.spread)}
        </td>
      </tr>
    ));

  const renderBody = () => {
    if (isLoading) {
      return renderLoadingRows();
    }

    if (error) {
      return renderError();
    }

    return renderMarketQuotes();
  };

  return (
    <section className="market-watch">
      <div className="market-watch__header">Market Watch</div>

      <table className="data-table">
        <thead>
          <tr>
            <th className="symbol-column">Symbol</th>
            <th className="numeric-column">Bid</th>
            <th className="numeric-column">Ask</th>
            <th className="numeric-column">Spread</th>
          </tr>
        </thead>

        <tbody>{renderBody()}</tbody>
      </table>
    </section>
  );
};

export default MarketWatch;
