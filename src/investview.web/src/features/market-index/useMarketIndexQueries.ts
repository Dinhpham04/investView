import { useQueries, useQuery } from '@tanstack/react-query';
import { getIndexOhlc, getMarketIndices } from '../../shared/api/marketApi';
import type { OhlcBar } from '../../shared/types/market';
import { defaultMarketIndexNames } from './marketIndexLists';

const vietnamTimeZone = 'Asia/Ho_Chi_Minh';
const sessionStartMinute = 9 * 60;
const sessionEndMinute = 15 * 60;
const marketIndexOhlcBackfillRefetchIntervalMs = 60_000;

export function useMarketIndexQueries(indexNames: readonly string[] = defaultMarketIndexNames) {
  const marketSessionRange = createVietnamFullMarketSessionRange();
  const indicesQuery = useQuery({
    queryKey: ['market-indices', indexNames],
    queryFn: () => getMarketIndices({ names: [...indexNames] }),
    staleTime: 30_000,
  });
  const ohlcQueries = useQueries({
    queries: indexNames.map((indexName) => ({
      queryKey: [
        'market-index-ohlc',
        indexName,
        '1',
        marketSessionRange.from.toISOString(),
        marketSessionRange.to.toISOString(),
      ],
      queryFn: () => {
        const backfillRange = createVietnamMarketSessionRange();

        return getIndexOhlc({
          from: backfillRange.from.toISOString(),
          resolution: '1',
          symbol: indexName,
          to: backfillRange.to.toISOString(),
        });
      },
      refetchInterval: () => getMarketIndexOhlcBackfillRefetchInterval(),
      refetchOnReconnect: false,
      refetchOnWindowFocus: false,
      staleTime: marketIndexOhlcBackfillRefetchIntervalMs,
    })),
  });
  const ohlcByIndexName = new Map<string, OhlcBar[]>();

  indexNames.forEach((indexName, index) => {
    ohlcByIndexName.set(indexName, filterVietnamMarketSessionBars(ohlcQueries[index].data ?? []));
  });

  return {
    indicesQuery,
    isOhlcPending: ohlcQueries.some((query) => query.isPending),
    ohlcByIndexName,
  };
}

export function createVietnamMarketSessionRange(now = new Date()) {
  const fullSessionRange = createVietnamFullMarketSessionRange(now);
  const currentTime = now.getTime();
  const sessionStartTime = fullSessionRange.from.getTime();
  const sessionEndTime = fullSessionRange.to.getTime();

  if (currentTime <= sessionStartTime) {
    return {
      from: fullSessionRange.from,
      to: fullSessionRange.from,
    };
  }

  if (currentTime >= sessionEndTime) {
    return fullSessionRange;
  }

  return {
    from: fullSessionRange.from,
    to: floorToMinute(now),
  };
}

export function createVietnamFullMarketSessionRange(now = new Date()) {
  const dateParts = getVietnamDateParts(now);

  return {
    from: new Date(`${dateParts.year}-${dateParts.month}-${dateParts.day}T09:00:00+07:00`),
    to: new Date(`${dateParts.year}-${dateParts.month}-${dateParts.day}T15:00:00+07:00`),
  };
}

export function getMarketIndexOhlcBackfillRefetchInterval(now = new Date()) {
  const fullSessionRange = createVietnamFullMarketSessionRange(now);
  const currentTime = now.getTime();

  return currentTime >= fullSessionRange.from.getTime() && currentTime <= fullSessionRange.to.getTime()
    ? marketIndexOhlcBackfillRefetchIntervalMs
    : false;
}

export function filterVietnamMarketSessionBars(bars: OhlcBar[]) {
  return bars.filter((bar) => {
    const barTime = new Date(bar.time);
    if (Number.isNaN(barTime.getTime())) {
      return false;
    }

    const { hour, minute } = getVietnamTimeParts(barTime);
    const minuteOfDay = hour * 60 + minute;

    return minuteOfDay >= sessionStartMinute && minuteOfDay <= sessionEndMinute;
  });
}

function getVietnamDateParts(date: Date) {
  const parts = new Intl.DateTimeFormat('en-US', {
    day: '2-digit',
    month: '2-digit',
    timeZone: vietnamTimeZone,
    year: 'numeric',
  }).formatToParts(date);

  return {
    day: getDateTimePart(parts, 'day'),
    month: getDateTimePart(parts, 'month'),
    year: getDateTimePart(parts, 'year'),
  };
}

function getVietnamTimeParts(date: Date) {
  const parts = new Intl.DateTimeFormat('en-US', {
    hour: '2-digit',
    hourCycle: 'h23',
    minute: '2-digit',
    timeZone: vietnamTimeZone,
  }).formatToParts(date);

  return {
    hour: Number.parseInt(getDateTimePart(parts, 'hour'), 10),
    minute: Number.parseInt(getDateTimePart(parts, 'minute'), 10),
  };
}

function floorToMinute(date: Date) {
  const floored = new Date(date);
  floored.setSeconds(0, 0);
  return floored;
}

function getDateTimePart(parts: Intl.DateTimeFormatPart[], type: Intl.DateTimeFormatPartTypes) {
  return parts.find((part) => part.type === type)?.value ?? '';
}
