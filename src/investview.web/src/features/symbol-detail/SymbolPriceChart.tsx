import { useEffect, useMemo, useRef } from 'react';
import {
  CandlestickSeries,
  ColorType,
  HistogramSeries,
  type BarPrice,
  createChart,
  type CandlestickData,
  type HistogramData,
  type LogicalRangeChangeEventHandler,
  type UTCTimestamp,
} from 'lightweight-charts';
import { formatChartPrice, formatCompactQuantity } from './symbolChartFormatters';
import type { ChartTimeframe } from './useSymbolDetailQueries';
import type { OhlcBar, SymbolDetail } from '../../shared/types/market';

type SymbolPriceChartProps = {
  bars: OhlcBar[];
  canLoadMoreHistory: boolean;
  detail: SymbolDetail;
  isError: boolean;
  isLoadingMoreHistory: boolean;
  isPending: boolean;
  onLoadMoreHistory: () => void;
  onTimeframeChange: (timeframe: ChartTimeframe) => void;
  selectedTimeframe: ChartTimeframe;
  timeframes: ChartTimeframe[];
};

const upColor = '#00d060';
const downColor = '#e60028';
const gridColor = '#24242d';
const historyLoadThreshold = 15;
const textColor = '#b8b8c6';

export function SymbolPriceChart({
  bars,
  canLoadMoreHistory,
  detail,
  isError,
  isLoadingMoreHistory,
  isPending,
  onLoadMoreHistory,
  onTimeframeChange,
  selectedTimeframe,
  timeframes,
}: SymbolPriceChartProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const canLoadMoreHistoryRef = useRef(canLoadMoreHistory);
  const candleSeriesRef = useRef<{ setData: (data: CandlestickData<UTCTimestamp>[]) => void } | null>(null);
  const chartRef = useRef<ReturnType<typeof createChart> | null>(null);
  const lastHistoryLoadAtRef = useRef(0);
  const onLoadMoreHistoryRef = useRef(onLoadMoreHistory);
  const previousFirstTimeRef = useRef<UTCTimestamp | null>(null);
  const shouldFitContentRef = useRef(true);
  const volumeSeriesRef = useRef<{ setData: (data: HistogramData<UTCTimestamp>[]) => void } | null>(null);
  const chartData = useMemo(() => createChartData(bars), [bars]);

  useEffect(() => {
    canLoadMoreHistoryRef.current = canLoadMoreHistory;
    onLoadMoreHistoryRef.current = onLoadMoreHistory;
  }, [canLoadMoreHistory, onLoadMoreHistory]);

  useEffect(() => {
    previousFirstTimeRef.current = null;
    shouldFitContentRef.current = true;
  }, [detail.symbol, selectedTimeframe.id]);

  useEffect(() => {
    const container = containerRef.current;
    if (container == null) {
      return;
    }

    const chart = createChart(container, {
      autoSize: true,
      layout: {
        background: { type: ColorType.Solid, color: '#030304' },
        textColor,
      },
      grid: {
        horzLines: { color: gridColor, style: 2 },
        vertLines: { color: gridColor, style: 2 },
      },
      rightPriceScale: {
        borderColor: '#33333c',
        scaleMargins: {
          top: 0.08,
          bottom: 0.24,
        },
      },
      timeScale: {
        borderColor: '#33333c',
        rightOffset: 8,
        timeVisible: true,
        secondsVisible: false,
      },
      crosshair: {
        horzLine: { color: '#00a887', labelBackgroundColor: '#009879' },
        vertLine: { color: '#5b5d68', labelBackgroundColor: '#2f3038' },
      },
      localization: {
        priceFormatter: formatChartPrice,
      },
    });

    const candleSeries = chart.addSeries(CandlestickSeries, {
      borderDownColor: downColor,
      borderUpColor: upColor,
      downColor,
      wickDownColor: downColor,
      wickUpColor: upColor,
      upColor,
    });

    const volumeSeries = chart.addSeries(HistogramSeries, {
      color: '#00a887',
      priceFormat: {
        formatter: formatCompactQuantity,
        minMove: 1,
        tickmarksFormatter: (values: BarPrice[]) => values.map(formatCompactQuantity),
        type: 'custom',
      },
      priceScaleId: '',
    });
    volumeSeries.priceScale().applyOptions({
      scaleMargins: {
        top: 0.78,
        bottom: 0,
      },
    });

    const visibleLogicalRangeHandler: LogicalRangeChangeEventHandler = (range) => {
      if (range == null || range.from > historyLoadThreshold) {
        return;
      }

      const now = Date.now();
      if (!canLoadMoreHistoryRef.current || now - lastHistoryLoadAtRef.current < 700) {
        return;
      }

      lastHistoryLoadAtRef.current = now;
      onLoadMoreHistoryRef.current();
    };

    chart.timeScale().subscribeVisibleLogicalRangeChange(visibleLogicalRangeHandler);
    chartRef.current = chart;
    candleSeriesRef.current = candleSeries;
    volumeSeriesRef.current = volumeSeries;

    return () => {
      chart.timeScale().unsubscribeVisibleLogicalRangeChange(visibleLogicalRangeHandler);
      chart.remove();
      candleSeriesRef.current = null;
      chartRef.current = null;
      volumeSeriesRef.current = null;
    };
  }, []);

  useEffect(() => {
    const chart = chartRef.current;
    const candleSeries = candleSeriesRef.current;
    const volumeSeries = volumeSeriesRef.current;
    if (chart == null || candleSeries == null || volumeSeries == null) {
      return;
    }

    const previousFirstTime = previousFirstTimeRef.current;
    const currentFirstTime = chartData.candles[0]?.time ?? null;
    const currentRange = shouldFitContentRef.current ? null : chart.timeScale().getVisibleLogicalRange();
    const prependedBars = previousFirstTime == null
      ? 0
      : chartData.candles.findIndex((candle) => candle.time === previousFirstTime);

    candleSeries.setData(chartData.candles);
    volumeSeries.setData(chartData.volumes);

    if (chartData.candles.length > 0) {
      if (shouldFitContentRef.current) {
        chart.timeScale().fitContent();
        shouldFitContentRef.current = false;
      } else if (currentRange != null && prependedBars > 0) {
        chart.timeScale().setVisibleLogicalRange({
          from: currentRange.from + prependedBars,
          to: currentRange.to + prependedBars,
        });
      }
    }

    previousFirstTimeRef.current = currentFirstTime;
  }, [chartData]);

  return (
    <section className="flex min-h-0 flex-col border-r border-[#32303d] bg-[#030304]">
      <div className="flex h-10 shrink-0 items-center justify-between border-b border-[#32303d] bg-black px-3">
        <div className="flex min-w-0 items-center gap-3">
          <div className="flex items-center gap-1.5 text-sm font-bold text-white">
            <span>{detail.symbol}</span>
            <span className="text-[#a6a6b4]">· {selectedTimeframe.label} · {detail.marketId}</span>
          </div>
          <span className="size-4 rounded-full bg-[#2c2d35] text-center text-xs leading-4 text-[#a6a6b4]">−</span>
          <div className="hidden gap-2 text-xs font-semibold tabular-nums text-[#00d060] md:flex">
            <span>O{formatChartPrice(lastBar(chartData.candles)?.open)}</span>
            <span>H{formatChartPrice(lastBar(chartData.candles)?.high)}</span>
            <span>L{formatChartPrice(lastBar(chartData.candles)?.low)}</span>
            <span>C{formatChartPrice(lastBar(chartData.candles)?.close)}</span>
            <span>{formatChangeText(detail.change, detail.changePercent)}</span>
          </div>
        </div>
        <div className="flex items-center gap-3 text-xs font-semibold text-white">
          <span className="hidden text-[#a6a6b4] lg:inline">Guest_94059...</span>
          <button className="size-7 border border-[#3a3946] bg-[#0d0d11] text-sm" type="button" title="Refresh chart">↻</button>
          <button className="size-7 border border-[#3a3946] bg-[#0d0d11] text-sm" type="button" title="Chart settings">⌾</button>
          <button className="size-7 border border-[#3a3946] bg-[#0d0d11] text-sm" type="button" title="Fullscreen">⛶</button>
        </div>
      </div>

      <div className="flex min-h-0 flex-1">
        <ChartToolRail />
        <div className="relative min-h-0 flex-1">
          <div ref={containerRef} className="absolute inset-0" data-testid="symbol-price-chart" />
          {isPending ? <ChartState label="Đang tải biểu đồ" /> : null}
          {isError ? <ChartState label="Không tải được biểu đồ" tone="error" /> : null}
          {!isPending && !isError && chartData.candles.length === 0 ? <ChartState label="Không có dữ liệu biểu đồ" /> : null}
          {isLoadingMoreHistory ? (
            <div className="absolute left-3 top-3 bg-black/75 px-2 py-1 text-xs font-semibold text-[#d7d7df]">
              Đang tải lịch sử
            </div>
          ) : null}
          {chartData.latestVolume != null ? (
            <div className="absolute bottom-16 left-3 text-xs font-semibold text-white">
              Khối lượng <span className="ml-2 text-[#00b89a]">{formatCompactQuantity(chartData.latestVolume)}</span>
            </div>
          ) : null}
        </div>
      </div>

      <div className="flex h-9 shrink-0 items-center justify-between border-t border-[#32303d] bg-black px-3 text-xs font-semibold">
        <div className="flex items-center gap-4 text-white">
          {timeframes.map((timeframe) => (
            <button
              key={timeframe.id}
              className={timeframe.id === selectedTimeframe.id ? 'text-white' : 'text-[#a6a6b4] hover:text-white'}
              type="button"
              onClick={() => onTimeframeChange(timeframe)}
            >
              {timeframe.label}
            </button>
          ))}
        </div>
        <div className="hidden items-center gap-3 text-white md:flex">
          <span>{formatVietnamTime(new Date())} (UTC+7)</span>
          <span>%</span>
          <span>log</span>
          <span>tự động</span>
        </div>
      </div>
    </section>
  );
}

function ChartToolRail() {
  return (
    <div className="flex w-14 shrink-0 flex-col items-center border-r border-[#1d1d24] bg-black py-3 text-[#d7d7df]">
      {['＋', '╱', '═', '⌬', '☍', '⌒', 'T', '☻', '▱', '⊕', '∩', '✎', '▣', '◎'].map((item) => (
        <button key={item} className="mb-2 size-8 text-lg leading-8 hover:bg-[#1d1d24]" type="button">
          {item}
        </button>
      ))}
    </div>
  );
}

function ChartState({ label, tone = 'muted' }: { label: string; tone?: 'muted' | 'error' }) {
  return (
    <div className={`absolute inset-0 grid place-items-center bg-black/65 text-xs font-semibold ${tone === 'error' ? 'text-state-error' : 'text-market-text-muted'}`}>
      {label}
    </div>
  );
}

function createChartData(bars: OhlcBar[]) {
  const validBars = bars
    .filter((bar) => bar.open != null && bar.high != null && bar.low != null && bar.close != null)
    .map((bar) => ({
      close: bar.close as number,
      high: bar.high as number,
      low: bar.low as number,
      open: bar.open as number,
      time: toUtcTimestamp(bar.time),
      volume: bar.volume ?? 0,
    }))
    .filter((bar): bar is { close: number; high: number; low: number; open: number; time: UTCTimestamp; volume: number } => bar.time != null)
    .sort((left, right) => left.time - right.time);

  const candles: CandlestickData<UTCTimestamp>[] = validBars.map((bar) => ({
    close: bar.close,
    high: bar.high,
    low: bar.low,
    open: bar.open,
    time: bar.time,
  }));
  const volumes: HistogramData<UTCTimestamp>[] = validBars.map((bar) => ({
    color: bar.close >= bar.open ? 'rgba(0, 184, 154, 0.75)' : 'rgba(214, 38, 54, 0.72)',
    time: bar.time,
    value: bar.volume,
  }));

  return {
    candles,
    latestVolume: validBars.at(-1)?.volume ?? null,
    volumes,
  };
}

function toUtcTimestamp(value: string): UTCTimestamp | null {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return Math.floor(date.getTime() / 1000) as UTCTimestamp;
}

function lastBar<T>(items: T[]) {
  return items.length === 0 ? null : items[items.length - 1];
}

function formatChangeText(change: number | null, changePercent: number | null) {
  const signedChange = change == null ? '-' : `${change > 0 ? '+' : ''}${change.toFixed(2)}`;
  const signedPercent = changePercent == null ? '-' : `${changePercent > 0 ? '+' : ''}${changePercent.toFixed(2)}%`;
  return `${signedChange} (${signedPercent})`;
}

function formatVietnamTime(value: Date) {
  return value.toLocaleTimeString('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    timeZone: 'Asia/Ho_Chi_Minh',
  });
}
