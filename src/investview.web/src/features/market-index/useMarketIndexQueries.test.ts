import { describe, expect, it } from 'vitest';
import {
  createVietnamFullMarketSessionRange,
  createVietnamMarketSessionRange,
  filterVietnamMarketSessionBars,
  getMarketIndexOhlcBackfillRefetchInterval,
} from './useMarketIndexQueries';
import type { OhlcBar } from '../../shared/types/market';

describe('market index OHLC session range', () => {
  it('uses the full Vietnam trading session after market close', () => {
    const range = createVietnamMarketSessionRange(new Date('2026-07-03T08:45:00.000Z'));

    expect(range.from.toISOString()).toBe('2026-07-03T02:00:00.000Z');
    expect(range.to.toISOString()).toBe('2026-07-03T08:00:00.000Z');
  });

  it('caps the Vietnam trading session to the current minute during market hours', () => {
    const range = createVietnamMarketSessionRange(new Date('2026-07-03T04:05:18.000Z'));

    expect(range.from.toISOString()).toBe('2026-07-03T02:00:00.000Z');
    expect(range.to.toISOString()).toBe('2026-07-03T04:05:00.000Z');
  });

  it('keeps the backfill range stable within the same minute', () => {
    const firstRange = createVietnamMarketSessionRange(new Date('2026-07-03T04:05:18.000Z'));
    const secondRange = createVietnamMarketSessionRange(new Date('2026-07-03T04:05:52.000Z'));

    expect(firstRange.to.toISOString()).toBe(secondRange.to.toISOString());
  });

  it('does not request future data before market open', () => {
    const range = createVietnamMarketSessionRange(new Date('2026-07-03T01:30:00.000Z'));

    expect(range.from.toISOString()).toBe('2026-07-03T02:00:00.000Z');
    expect(range.to.toISOString()).toBe('2026-07-03T02:00:00.000Z');
  });

  it('keeps a full-session range for chart geometry', () => {
    const range = createVietnamFullMarketSessionRange(new Date('2026-07-03T04:05:18.000Z'));

    expect(range.from.toISOString()).toBe('2026-07-03T02:00:00.000Z');
    expect(range.to.toISOString()).toBe('2026-07-03T08:00:00.000Z');
  });

  it('enables one-minute backfill polling throughout the Vietnam trading session', () => {
    expect(getMarketIndexOhlcBackfillRefetchInterval(new Date('2026-07-03T02:00:00.000Z'))).toBe(60_000);
    expect(getMarketIndexOhlcBackfillRefetchInterval(new Date('2026-07-03T04:30:00.000Z'))).toBe(60_000);
    expect(getMarketIndexOhlcBackfillRefetchInterval(new Date('2026-07-03T05:00:00.000Z'))).toBe(60_000);
    expect(getMarketIndexOhlcBackfillRefetchInterval(new Date('2026-07-03T08:00:00.000Z'))).toBe(60_000);
  });

  it('disables one-minute backfill polling outside the Vietnam trading session', () => {
    expect(getMarketIndexOhlcBackfillRefetchInterval(new Date('2026-07-03T01:59:59.000Z'))).toBe(false);
    expect(getMarketIndexOhlcBackfillRefetchInterval(new Date('2026-07-03T08:00:01.000Z'))).toBe(false);
  });

  it('filters index OHLC bars to the Vietnam trading session only', () => {
    const bars: OhlcBar[] = [
      createBar('2026-07-03T01:59:00.000Z'),
      createBar('2026-07-03T02:00:00.000Z'),
      createBar('2026-07-03T07:30:00.000Z'),
      createBar('2026-07-03T08:00:00.000Z'),
      createBar('2026-07-03T08:01:00.000Z'),
    ];

    expect(filterVietnamMarketSessionBars(bars).map((bar) => bar.time)).toEqual([
      '2026-07-03T02:00:00.000Z',
      '2026-07-03T07:30:00.000Z',
      '2026-07-03T08:00:00.000Z',
    ]);
  });
});

function createBar(time: string): OhlcBar {
  return {
    close: 1,
    high: 1,
    low: 1,
    open: 1,
    resolution: '1',
    symbol: 'VNINDEX',
    time,
    volume: 1,
  };
}
