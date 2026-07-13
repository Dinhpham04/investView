import type { MarketOhlcUpdate, OhlcBar } from '../../shared/types/market';

export function mergeIndexOhlcUpdate(
  ohlcByIndexName: Map<string, OhlcBar[]>,
  update: MarketOhlcUpdate,
): Map<string, OhlcBar[]> {
  const normalizedIndexName = update.symbol.trim().toUpperCase();
  const nextBars = new Map(Array.from(ohlcByIndexName, ([indexName, bars]) => [indexName, [...bars]]));
  const bars = nextBars.get(normalizedIndexName) ?? [];
  const updateBar: OhlcBar = {
    close: update.close,
    high: update.high,
    low: update.low,
    open: update.open,
    resolution: update.resolution,
    symbol: normalizedIndexName,
    time: update.time,
    volume: update.volume,
  };
  const existingIndex = bars.findIndex((bar) => bar.time === updateBar.time && bar.resolution === updateBar.resolution);
  const mergedBars = existingIndex >= 0
    ? bars.map((bar, index) => (index === existingIndex ? updateBar : bar))
    : [...bars, updateBar];

  nextBars.set(
    normalizedIndexName,
    mergedBars.sort((left, right) => new Date(left.time).getTime() - new Date(right.time).getTime()),
  );

  return nextBars;
}
