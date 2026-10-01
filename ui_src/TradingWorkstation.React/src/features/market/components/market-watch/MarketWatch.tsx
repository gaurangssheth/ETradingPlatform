import { useEffect, useState } from "react";
import type { MarketQuote } from "../../models/MarketQuote";
import { getMarketQuotes } from "../../services/marketApi";
import { formatMarketPrice } from "../../utils/formatMarketPrice";
import { useAppSelector, userAppDisptch } from "../../../../store/hooks";
import { selectedSymbolSelector, selectSymbol } from "../../state/marketSlice";
import type { MarketPriceDirection } from "../../models/MarketPriceDirection";
import "./MarketWatch.scss";
import "./MarketWatchSkeleton.scss";
import { useMarketDataHubConnection } from "../../context/useMarketDataHubConnection";
import { MarketWatchSkeleton } from "./MarketWatchSkeleton";

export const MarketWatch = () => {
  const [quotes, setQuotes] = useState<MarketQuote[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [priceDirections, setPriceDirections] = useState<
    Record<string, MarketPriceDirection>
  >({});
  const { connection } = useMarketDataHubConnection();

  const dispatch = userAppDisptch();
  const selectedSymbol = useAppSelector(selectedSymbolSelector);

  useEffect(() => {
    let cancelled = false;

    const loadMarketQuotes = async () => {
      try {
        const result = await getMarketQuotes();
        if (!cancelled) {
          setQuotes(result);
          setError(null);
        }
      } catch {
        if (!cancelled) {
          setError("Unable to load market quotes.");
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    };
    void loadMarketQuotes();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!connection) {
      return;
    }

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

    return () => {
      connection.off("MarketQuoteUpdated", handleMarketQuoteUpdated);
    };
  }, [connection]);

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
      return <MarketWatchSkeleton />;
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
