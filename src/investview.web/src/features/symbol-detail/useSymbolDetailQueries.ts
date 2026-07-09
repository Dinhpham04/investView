import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { getLatestTrades, getOhlc, getSymbolDetail } from '../../shared/api/marketApi';
import type { OhlcBar } from '../../shared/types/market';

export type SymbolDetailSelection = {
  boardId: string;
  symbol: string;
};

export type ChartTimeframeId = '1m' | '3m' | '5m' | '15m' | '30m' | '1h' | '4h' | '1d' | '1w';

export type ChartTimeframe = {
  aggregationMinutes?: number;
  id: ChartTimeframeId;
  label: string;
  lookbackDays: number;
  resolution: string;
};

type OhlcRange = {
  from: string;
  to: string;
};

export type OhlcHistoryPage = {
  bars: OhlcBar[];
  nextRange?: OhlcRange;
  range: OhlcRange;
};

export const chartTimeframes: ChartTimeframe[] = [
  { id: '1m', label: '1m', lookbackDays: 1, resolution: '1' },
  { id: '3m', label: '3m', lookbackDays: 3, resolution: '3' },
  { id: '5m', label: '5m', lookbackDays: 5, resolution: '5' },
  { id: '15m', label: '15m', lookbackDays: 14, resolution: '15' },
  { id: '30m', label: '30m', lookbackDays: 30, resolution: '30' },
  { id: '1h', label: '1H', lookbackDays: 90, resolution: '1H' },
  { id: '4h', label: '4H', lookbackDays: 180, resolution: '1H', aggregationMinutes: 240 },
  { id: '1d', label: '1D', lookbackDays: 730, resolution: '1D' },
  { id: '1w', label: '1W', lookbackDays: 1825, resolution: '1W' },
];

export const defaultChartTimeframe = chartTimeframes.find((timeframe) => timeframe.id === '1d') ?? chartTimeframes[0];

export function useSymbolDetailQueries(selection: SymbolDetailSelection | null, chartTimeframe: ChartTimeframe = defaultChartTimeframe) {
  const enabled = selection != null;

  const detailQuery = useQuery({
    enabled,
    queryKey: ['symbol-detail', selection?.boardId, selection?.symbol],
    queryFn: () => getSymbolDetail({ boardId: selection!.boardId, symbol: selection!.symbol }),
  });

  const ohlcQuery = useInfiniteQuery({
    enabled,
    getNextPageParam: (lastPage: OhlcHistoryPage) => lastPage.nextRange,
    initialPageParam: createChartRange(chartTimeframe),
    queryKey: ['symbol-ohlc', selection?.symbol, chartTimeframe.id, chartTimeframe.resolution],
    queryFn: async ({ pageParam }): Promise<OhlcHistoryPage> => {
      const bars = await getOhlc({
        from: pageParam.from,
        resolution: chartTimeframe.resolution,
        symbol: selection!.symbol,
        to: pageParam.to,
      });

      return createOhlcHistoryPage(pageParam, bars, chartTimeframe);
    },
  });

  const latestTradesQuery = useQuery({
    enabled,
    queryKey: ['symbol-latest-trades', selection?.boardId, selection?.symbol, 30],
    queryFn: () => getLatestTrades({ boardId: selection!.boardId, limit: 30, symbol: selection!.symbol }),
  });

  return {
    detailQuery,
    latestTradesQuery,
    ohlcQuery,
  };
}

function createChartRange(timeframe: ChartTimeframe): OhlcRange {
  const to = new Date();
  const from = new Date(to.getTime() - timeframe.lookbackDays * 24 * 60 * 60 * 1000);

  return {
    from: from.toISOString(),
    to: to.toISOString(),
  };
}

function createOhlcHistoryPage(range: OhlcRange, bars: OhlcBar[], timeframe: ChartTimeframe): OhlcHistoryPage {
  return {
    bars,
    nextRange: createPreviousChartRange(bars, timeframe),
    range,
  };
}

function createPreviousChartRange(bars: OhlcBar[], timeframe: ChartTimeframe): OhlcRange | undefined {
  const earliestTime = getEarliestBarTime(bars);
  if (earliestTime == null) {
    return undefined;
  }

  const to = new Date(earliestTime - 1);
  const from = new Date(to.getTime() - timeframe.lookbackDays * 24 * 60 * 60 * 1000);

  return {
    from: from.toISOString(),
    to: to.toISOString(),
  };
}

function getEarliestBarTime(bars: OhlcBar[]) {
  return bars.reduce<number | null>((earliest, bar) => {
    const time = new Date(bar.time).getTime();
    if (Number.isNaN(time)) {
      return earliest;
    }

    return earliest == null ? time : Math.min(earliest, time);
  }, null);
}

export function mergeOhlcHistoryPages(pages: OhlcHistoryPage[] | undefined): OhlcBar[] {
  const barsByTime = new Map<string, OhlcBar>();
  for (const page of pages ?? []) {
    for (const bar of page.bars) {
      const time = new Date(bar.time).getTime();
      if (!Number.isNaN(time)) {
        barsByTime.set(bar.time, bar);
      }
    }
  }

  return Array.from(barsByTime.values())
    .sort((left, right) => new Date(left.time).getTime() - new Date(right.time).getTime());
}

export function aggregateOhlcBarsForTimeframe(bars: OhlcBar[], timeframe: ChartTimeframe): OhlcBar[] {
  if (timeframe.aggregationMinutes == null) {
    return bars;
  }

  const bucketSizeMs = timeframe.aggregationMinutes * 60 * 1000;
  const buckets = new Map<number, OhlcBar[]>();
  for (const bar of bars) {
    const time = new Date(bar.time).getTime();
    if (
      Number.isNaN(time) ||
      bar.open == null ||
      bar.high == null ||
      bar.low == null ||
      bar.close == null
    ) {
      continue;
    }

    const bucketTime = Math.floor(time / bucketSizeMs) * bucketSizeMs;
    buckets.set(bucketTime, [...(buckets.get(bucketTime) ?? []), bar]);
  }

  return Array.from(buckets.entries())
    .sort(([leftTime], [rightTime]) => leftTime - rightTime)
    .map(([bucketTime, bucketBars]) => {
      const orderedBars = bucketBars.sort((left, right) => new Date(left.time).getTime() - new Date(right.time).getTime());
      const firstBar = orderedBars[0];
      const lastBar = orderedBars[orderedBars.length - 1];

      return {
        ...firstBar,
        close: lastBar.close,
        high: Math.max(...orderedBars.map((bar) => bar.high ?? Number.NEGATIVE_INFINITY)),
        low: Math.min(...orderedBars.map((bar) => bar.low ?? Number.POSITIVE_INFINITY)),
        open: firstBar.open,
        resolution: timeframe.label,
        time: new Date(bucketTime).toISOString(),
        volume: orderedBars.reduce((total, bar) => total + (bar.volume ?? 0), 0),
      };
    });
}
