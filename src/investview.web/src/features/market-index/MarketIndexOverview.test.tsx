import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { MiniIndexChart, createMiniChartGeometry } from './MarketIndexOverview';
import type { OhlcBar } from '../../shared/types/market';

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
