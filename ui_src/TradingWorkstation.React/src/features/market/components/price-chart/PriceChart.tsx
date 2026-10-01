import { useEffect, useState } from "react";
import { useMarketDataHubConnection } from "../../context/useMarketDataHubConnection";
import type { MarketCandle } from "../../models/MarketCandle";
import { useAppSelector } from "../../../../store/hooks";
import { selectedSymbolSelector } from "../../state/marketSlice";
import { getMarketCandles } from "../../services/marketApi";
import { mergeMarketCandle } from "../../utils/mergeMarketCandle";
import "./PriceChart.scss";
import CandlestickChart from "./CandlestickChart";
import { PriceChartSkeleton } from "./PriceChartSkeleton";

type PriceChartContentProps = {
  symbol: string;
};

export const PriceChartContent = ({ symbol }: PriceChartContentProps) => {
  const { connection } = useMarketDataHubConnection();
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [candles, setCandles] = useState<MarketCandle[]>([]);

  useEffect(() => {
    let cancelled = false;
    const loadCandles = async (symbol: string) => {
      try {
        const result = await getMarketCandles(symbol);
        if (!cancelled) {
          setCandles((current) => {
            const liveCandles = current.filter(
              (candle) => candle.symbol === symbol,
            );

            return liveCandles.reduce(
              (merged, candle) => mergeMarketCandle(merged, candle),
              result,
            );
          });
          setError(null);
        }
      } catch {
        if (!cancelled) {
          setError(`Unable to load candles for ${symbol}.`);
        }
      } finally {
        setIsLoading(false);
      }
    };

    void loadCandles(symbol);

    return () => {
      cancelled = true;
    };
  }, [symbol]);

  useEffect(() => {
    if (!connection) {
      return;
    }

    const handleMarketCandleUpdated = (candle: MarketCandle) => {
      if (candle.symbol !== symbol) {
        return;
      }

      setCandles((current) =>
        mergeMarketCandle(
          current.filter((item) => item.symbol === symbol),
          candle,
        ),
      );
    };

    connection.on("MarketCandleUpdated", handleMarketCandleUpdated);

    return () => {
      connection.off("MarketCandleUpdated", handleMarketCandleUpdated);
    };
  }, [connection, symbol]);

  const displayedCandles = candles.filter((candle) => candle.symbol === symbol);

  const renderBody = () => {
    if (isLoading && displayedCandles.length === 0) {
      return <PriceChartSkeleton />;
    }

    if (error && displayedCandles.length === 0) {
      return <div className="price-chart__error">{error}</div>;
    }

    if (displayedCandles.length === 0) {
      return (
        <div className="price-chart__empty">No candle data available.</div>
      );
    }

    return <CandlestickChart candles={displayedCandles} />;
  };

  return (
    <section className="price-chart">
      <div className="price-chart__header">{symbol} — Price Chart</div>

      <div className="price-chart__content">{renderBody()}</div>
    </section>
  );
};

export const PriceChart = () => {
  const selectedSymbol = useAppSelector(selectedSymbolSelector);

  return <PriceChartContent key={selectedSymbol} symbol={selectedSymbol} />;
};

export default PriceChart;
