import { useEffect, useRef } from "react";
import type { MarketCandle } from "../../models/MarketCandle";
import {
  CandlestickSeries,
  createChart,
  type CandlestickData,
  type IChartApi,
  type ISeriesApi,
  type UTCTimestamp,
} from "lightweight-charts";
import "./CandlestickChart.scss";

type CandlestickChartProps = {
  candles: MarketCandle[];
};

const INITIAL_VISIBLE_CANDLES = 25;

export const CandlestickChart = ({ candles }: CandlestickChartProps) => {
  const chartContainerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const seriesRef = useRef<ISeriesApi<"Candlestick"> | null>(null);
  const initialViewSetRef = useRef(false);

  // Create the chart once.
  useEffect(() => {
    if (!chartContainerRef.current) {
      return;
    }

    const chart = createChart(chartContainerRef.current, {
      autoSize: true,

      rightPriceScale: {
        visible: true,
        autoScale: true,
        borderVisible: true,
        scaleMargins: {
          top: 0.12,
          bottom: 0.12,
        },
      },

      leftPriceScale: {
        visible: false,
      },

      timeScale: {
        barSpacing: 16,
        rightOffset: 3,
        timeVisible: true,
        secondsVisible: true,
      },
    });

    chartRef.current = chart;

    seriesRef.current = chart.addSeries(CandlestickSeries, {
      priceFormat: {
        type: "price",
        precision: 4,
        minMove: 0.0001,
      },

      priceLineVisible: true,
      lastValueVisible: true,
    });

    return () => {
      seriesRef.current = null;
      chartRef.current = null;
      initialViewSetRef.current = false;
      chart.remove();
    };
  }, []);

  // Update chart data when candles change.
  useEffect(() => {
    const chartData: CandlestickData<UTCTimestamp>[] = candles.map(
      (candle) => ({
        time: (Date.parse(candle.startTime) / 1000) as UTCTimestamp,
        open: candle.open,
        high: candle.high,
        low: candle.low,
        close: candle.close,
      }),
    );

    const series = seriesRef.current;

    if (!series) {
      return;
    }

    seriesRef.current?.setData(chartData);

    if (!initialViewSetRef.current && chartData.length > 0) {
      chartRef.current?.timeScale().setVisibleLogicalRange({
        from: chartData.length - INITIAL_VISIBLE_CANDLES,
        to: chartData.length + 2,
      });

      initialViewSetRef.current = true;
    }
  }, [candles]);

  return <div ref={chartContainerRef} className="candlestick-chart" />;
};

export default CandlestickChart;
