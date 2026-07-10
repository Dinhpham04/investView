import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { MarketIndexTable, MiniIndexChart, createMiniChartGeometry } from './MarketIndexOverview';
import type { MarketIndex, OhlcBar } from '../../shared/types/market';

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

describe('market index table layout', () => {
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
