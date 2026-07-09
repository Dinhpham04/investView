import { describe, expect, it } from 'vitest';
import { createVietnamMarketSessionRange, filterVietnamMarketSessionBars } from './useMarketIndexQueries';
import type { OhlcBar } from '../../shared/types/market';

describe('market index OHLC session range', () => {
  it('uses the Vietnam trading session from 09:00 to 15:00 for the current Vietnam date', () => {
    const range = createVietnamMarketSessionRange(new Date('2026-07-03T07:45:00.000Z'));

    expect(range.from.toISOString()).toBe('2026-07-03T02:00:00.000Z');
    expect(range.to.toISOString()).toBe('2026-07-03T08:00:00.000Z');
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
