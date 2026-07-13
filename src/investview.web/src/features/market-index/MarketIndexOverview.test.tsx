import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { MarketIndexCard, MarketIndexTable, MiniIndexChart, createMiniChartGeometry } from './MarketIndexOverview';
import { mergeIndexOhlcUpdate } from './marketIndexOhlcRealtime';
import type { MarketIndex, MarketOhlcUpdate, OhlcBar } from '../../shared/types/market';

describe('market index mini chart geometry', () => {
  it('maps bars by Vietnam market time from 09h to 15h instead of by array index', () => {
    const geometry = createMiniChartGeometry([
      createBar('2026-07-03T02:00:00.000Z', 100),
      createBar('2026-07-03T05:00:00.000Z', 101),
      createBar('2026-07-03T08:00:00.000Z', 102),
    ], 99);

    expect(geometry).not.toBeNull();
    expect(geometry?.points).toHaveLength(3);
    expect(geometry?.points[0].x).toBeCloseTo(4);
    expect(geometry?.points[1].x).toBeCloseTo(120);
    expect(geometry?.points[2].x).toBeCloseTo(236);
  });

  it('scales the previous close reference line with the price range', () => {
    const geometry = createMiniChartGeometry([
      createBar('2026-07-03T02:00:00.000Z', 95),
      createBar('2026-07-03T08:00:00.000Z', 105),
    ], 100);

    expect(geometry?.referenceLine).toMatchObject({
      label: '100.00',
      y: 30,
    });
  });

  it('ignores invalid and out-of-session bars', () => {
    const geometry = createMiniChartGeometry([
      createBar('2026-07-03T01:59:00.000Z', 90),
      createBar('not-a-date', 91),
      createBar('2026-07-03T02:00:00.000Z', 100),
      createBar('2026-07-03T08:01:00.000Z', 110),
    ], 100);

    expect(geometry?.points).toHaveLength(1);
    expect(geometry?.points[0].x).toBeCloseTo(4);
  });

  it('ignores cached index OHLC outliers that are incompatible with the reference value', () => {
    const geometry = createMiniChartGeometry([
      createBar('2026-07-03T02:00:00.000Z', 292.57),
      createBar('2026-07-03T05:30:00.000Z', 292_570),
      createBar('2026-07-03T08:00:00.000Z', 293.12),
    ], 303.76);

    expect(geometry?.points).toHaveLength(2);
    expect(geometry?.linePoints).not.toContain('292570');
  });

  it('colors the index line by its position relative to the previous close reference', () => {
    const { container } = render(
      <MiniIndexChart
        bars={[
          createBar('2026-07-03T02:00:00.000Z', 99),
          createBar('2026-07-03T05:00:00.000Z', 101),
        ]}
        referenceValue={100}
        toneClass="text-price-down"
      />,
    );

    const aboveReferenceLine = container.querySelector('[data-reference-zone="above"]');
    const belowReferenceLine = container.querySelector('[data-reference-zone="below"]');

    expect(aboveReferenceLine?.getAttribute('stroke')).toBe('#00d084');
    expect(belowReferenceLine?.getAttribute('stroke')).toBe('#ff1f46');
  });
});

describe('market index realtime OHLC merge', () => {
  it('replaces an existing 1-minute index bar for the same bucket', () => {
    const currentBars = new Map<string, OhlcBar[]>([
      ['VNINDEX', [createBar('2026-07-03T02:00:00.000Z', 100)]],
    ]);

    const merged = mergeIndexOhlcUpdate(currentBars, createOhlcUpdate({
      close: 101,
      high: 101,
      low: 101,
      open: 101,
      time: '2026-07-03T02:00:00.000Z',
    }));

    expect(merged.get('VNINDEX')).toHaveLength(1);
    expect(merged.get('VNINDEX')?.[0]).toMatchObject({
      close: 101,
      high: 101,
      low: 101,
      open: 101,
    });
  });

  it('appends new realtime index bars in time order', () => {
    const currentBars = new Map<string, OhlcBar[]>([
      ['VNINDEX', [createBar('2026-07-03T02:01:00.000Z', 101)]],
    ]);

    const merged = mergeIndexOhlcUpdate(currentBars, createOhlcUpdate({
      close: 100,
      time: '2026-07-03T02:00:00.000Z',
    }));

    expect(merged.get('VNINDEX')?.map((bar) => bar.time)).toEqual([
      '2026-07-03T02:00:00.000Z',
      '2026-07-03T02:01:00.000Z',
    ]);
  });
});

describe('market index table layout', () => {
  it('renders traded value on compact index cards', () => {
    render(
      <MarketIndexCard
        bars={[]}
        index={createIndex({
          totalValue: 12_345.678,
          totalVolume: 92_824_000,
        })}
        isLoading={false}
      />,
    );

    expect(screen.getByText('92,824,000 CP')).toBeInTheDocument();
    expect(screen.getByText('12,345.678 Tỷ')).toBeInTheDocument();
    expect(screen.queryByText('GTGD 12,345.678 tỷ')).not.toBeInTheDocument();
  });

  it('keeps the index value and change in one compact headline', () => {
    render(
      <MarketIndexCard
        bars={[]}
        index={createIndex()}
        isLoading={false}
      />,
    );

    const headline = screen.getByText('↓1,838.12 (-2.58 -0.14%)');

    expect(headline).toHaveClass('whitespace-nowrap');
    expect(headline).toHaveClass('text-price-down');
  });

  it('renders the market session label on compact index cards', () => {
    render(
      <MarketIndexCard
        bars={[]}
        index={createIndex({ tradingSessionId: '99' })}
        isLoading={false}
        sessionLabel="Da dong cua"
      />,
    );

    expect(screen.getByText('Da dong cua')).toBeInTheDocument();
    expect(screen.queryByText('Phiên: Da dong cua')).not.toBeInTheDocument();
    expect(screen.queryByText('99')).not.toBeInTheDocument();
  });

  it('renders table traded value without re-scaling values that are already in billions', () => {
    render(
      <MarketIndexTable
        indices={[createIndex({ totalValue: 12_345.678 })]}
        isError={false}
        isLoading={false}
      />,
    );

    expect(screen.getByText('12,345.678')).toBeInTheDocument();
  });

  it('renders market breadth counts in a stable three-column cell', () => {
    render(
      <MarketIndexTable
        indices={[
          createIndex({
            downCount: 138,
            noChangeCount: 59,
            upCount: 106,
          }),
        ]}
        isError={false}
        isLoading={false}
      />,
    );

    const breadthCell = screen.getByTestId('market-index-breadth-VNINDEX');

    expect(breadthCell).toHaveClass('grid-cols-[1fr_1fr_1fr]');
    expect(Array.from(breadthCell.children)).toHaveLength(3);
    Array.from(breadthCell.children).forEach((item) => {
      expect(item).toHaveClass('grid-cols-[10px_minmax(0,1fr)]');
    });
    expect(within(breadthCell).getByText('↑')).toBeInTheDocument();
    expect(within(breadthCell).getByText('106')).toBeInTheDocument();
    expect(within(breadthCell).getByText('▬')).toBeInTheDocument();
    expect(within(breadthCell).getByText('59')).toBeInTheDocument();
    expect(within(breadthCell).getByText('↓')).toBeInTheDocument();
    expect(within(breadthCell).getByText('138')).toBeInTheDocument();
  });
});

function createBar(time: string, close: number): OhlcBar {
  return {
    close,
    high: close,
    low: close,
    open: close,
    resolution: '1',
    symbol: 'VNINDEX',
    time,
    volume: close * 1_000,
  };
}

function createOhlcUpdate(overrides: Partial<MarketOhlcUpdate> = {}): MarketOhlcUpdate {
  return {
    close: 100,
    high: 100,
    isClosed: false,
    low: 100,
    open: 100,
    resolution: '1',
    symbol: 'VNINDEX',
    time: '2026-07-03T02:00:00.000Z',
    type: 'INDEX',
    updatedAt: '2026-07-03T02:00:02.000Z',
    volume: 1000,
    ...overrides,
  };
}

function createIndex(overrides: Partial<MarketIndex> = {}): MarketIndex {
  return {
    change: -2.58,
    changePercent: -0.14,
    ceilingCount: null,
    downCount: 138,
    floorCount: null,
    highValue: 1844.21,
    indexName: 'VNINDEX',
    lowValue: 1835.5,
    marketId: 'HOSE',
    noChangeCount: 59,
    referenceValue: 1840.7,
    totalValue: 0,
    totalVolume: 92_824_000,
    tradingSessionId: 'LO',
    upCount: 106,
    updatedAt: '2026-07-10T08:00:00.000Z',
    value: 1838.12,
    ...overrides,
  };
}
