import { useEffect, useId, useMemo, useState } from 'react';
import {
  createVietnamMarketSessionRange,
  filterVietnamMarketSessionBars,
  useMarketIndexQueries,
} from './useMarketIndexQueries';
import { defaultMarketIndexNames } from './marketIndexLists';
import type { MarketIndex, MarketIndexUpdate, OhlcBar } from '../../shared/types/market';

type MarketIndexOverviewProps = {
  latestUpdate?: MarketIndexUpdate | null;
};

export function MarketIndexOverview({ latestUpdate = null }: MarketIndexOverviewProps) {
  const { indicesQuery, isOhlcPending, ohlcByIndexName } = useMarketIndexQueries(defaultMarketIndexNames);
  const [liveIndices, setLiveIndices] = useState<MarketIndex[]>([]);

  useEffect(() => {
    setLiveIndices(indicesQuery.data ?? []);
  }, [indicesQuery.data]);

  useEffect(() => {
    if (latestUpdate == null) {
      return;
    }

    setLiveIndices((currentIndices) => mergeIndexUpdate(currentIndices, latestUpdate));
  }, [latestUpdate]);

  const indices = useMemo(() => orderIndices(liveIndices), [liveIndices]);

  return (
    <section className="grid min-h-[132px] grid-cols-1 gap-1 border-x border-t border-market-border bg-[#12101b] lg:grid-cols-[minmax(0,1fr)_minmax(360px,470px)]">
      <div className="grid min-w-0 grid-cols-1 gap-1 md:grid-cols-2 xl:grid-cols-5">
        {defaultMarketIndexNames.map((indexName) => (
          <MarketIndexCard
            bars={ohlcByIndexName.get(indexName) ?? []}
            index={indices.find((item) => item.indexName === indexName) ?? createEmptyIndex(indexName)}
            isLoading={indicesQuery.isPending || isOhlcPending}
            key={indexName}
          />
        ))}
      </div>
      <MarketIndexTable indices={indices} isError={indicesQuery.isError} isLoading={indicesQuery.isPending} />
    </section>
  );
}

function MarketIndexCard({ bars, index, isLoading }: { bars: OhlcBar[]; index: MarketIndex; isLoading: boolean }) {
  const toneClass = classForChange(index.change);

  return (
    <article className="min-w-0 border border-[#2d2a38] bg-[#1d1a2a]">
      <div className="h-[88px] border-b border-[#34313d] bg-[#070711]">
        <MiniIndexChart bars={bars} referenceValue={index.referenceValue} toneClass={toneClass} />
      </div>
      <div className="grid grid-cols-[1fr_auto] gap-x-2 px-2 py-1.5 text-[11px] font-semibold leading-tight">
        <div className="min-w-0">
          <div className="flex items-center gap-1 text-white">
            <span className="truncate">{index.indexName}</span>
            <span className="text-[#9d99ad]">⌄</span>
          </div>
          <div className="mt-0.5 text-[#d7d4e3]">{formatCompactVolume(index.totalVolume)} CP</div>
          <div className="mt-0.5 flex gap-2">
            <span className="text-price-up">↑ {formatCount(index.upCount)}</span>
            <span className="text-price-ref">▬ {formatCount(index.noChangeCount)}</span>
            <span className="text-price-down">↓ {formatCount(index.downCount)}</span>
          </div>
        </div>
        <div className="text-right tabular-nums">
          <div className={toneClass}>{isLoading && index.value == null ? '...' : formatIndexValue(index.value)}</div>
          <div className={toneClass}>{formatSigned(index.change)} ({formatPercent(index.changePercent)})</div>
          <div className="mt-0.5 text-[#d7d4e3]">{index.tradingSessionId || '-'}</div>
        </div>
      </div>
    </article>
  );
}

function MarketIndexTable({ indices, isError, isLoading }: { indices: MarketIndex[]; isError: boolean; isLoading: boolean }) {
  return (
    <div className="min-w-0 border border-[#2d2a38] bg-[#1d1a2a] text-[11px] font-semibold">
      <div className="grid h-7 grid-cols-[1.15fr_0.9fr_0.95fr_1fr_1fr_1.1fr] items-center gap-2 border-b border-[#34313d] bg-[#171421] px-2 text-[#f2f2f6]">
        <span>⚙ Chỉ số</span>
        <span className="text-right">Điểm</span>
        <span className="text-right">+ / -</span>
        <span className="text-right">KLGD (Triệu)</span>
        <span className="text-right">GTGD (Tỷ)</span>
        <span className="text-right">CK Tăng/Giảm</span>
      </div>
      {isLoading ? <div className="px-3 py-8 text-center text-market-text-muted">Đang tải chỉ số</div> : null}
      {isError ? <div className="px-3 py-8 text-center text-state-error">Không tải được chỉ số</div> : null}
      {!isLoading && !isError ? (
        <div>
          {indices.map((index) => (
            <div
              className="grid h-[25px] grid-cols-[1.15fr_0.9fr_0.95fr_1fr_1fr_1.1fr] items-center gap-2 border-b border-[#282534] px-2 tabular-nums last:border-b-0"
              key={index.indexName}
            >
              <span className="text-[#d7d4e3]">{index.indexName}</span>
              <span className={`text-right ${classForChange(index.change)}`}>{formatIndexValue(index.value)}</span>
              <span className={`text-right ${classForChange(index.change)}`}>{formatSigned(index.change)}</span>
              <span className="text-right text-[#d7d4e3]">{formatMillion(index.totalVolume)}</span>
              <span className="text-right text-[#d7d4e3]">{formatBillion(index.totalValue)}</span>
              <span className="text-right">
                <span className="text-price-up">↑ {formatCount(index.upCount)}</span>
                <span className="px-1 text-price-ref">▬ {formatCount(index.noChangeCount)}</span>
                <span className="text-price-down">↓ {formatCount(index.downCount)}</span>
              </span>
            </div>
          ))}
        </div>
      ) : null}
    </div>
  );
}

export function MiniIndexChart({ bars, referenceValue, toneClass }: { bars: OhlcBar[]; referenceValue: number | null; toneClass: string }) {
  const chartDomId = useId().replaceAll(':', '');
  const geometry = createMiniChartGeometry(bars, referenceValue);

  if (geometry == null) {
    return <div className="grid h-full place-items-center text-[11px] font-semibold text-market-text-muted">-</div>;
  }

  const aboveReferenceClipId = `${chartDomId}-above-reference`;
  const belowReferenceClipId = `${chartDomId}-below-reference`;
  const fallbackStroke = toneClass.includes('down') ? '#ff1f46' : '#00d084';

  return (
    <svg className="h-full w-full" viewBox="0 0 240 82" preserveAspectRatio="none" aria-hidden="true">
      {geometry.referenceLine != null ? (
        <defs>
          <clipPath id={aboveReferenceClipId}>
            <rect height={geometry.referenceLine.y} width={miniChart.width} x="0" y="0" />
          </clipPath>
          <clipPath id={belowReferenceClipId}>
            <rect height={miniChart.height - geometry.referenceLine.y} width={miniChart.width} x="0" y={geometry.referenceLine.y} />
          </clipPath>
        </defs>
      ) : null}
      {geometry.gridLines.map((line) => (
        <g key={line.label}>
          <line x1={line.x} x2={line.x} y1="6" y2="80" stroke="#242331" strokeWidth="0.7" />
          <text x={line.x} y="80" fill="#9d99ad" fontSize="7.5" fontWeight="700" textAnchor="middle">
            {line.label}
          </text>
        </g>
      ))}
      {geometry.volumeBars.map((bar) => (
        <rect fill="#4c6b9d" height={bar.height} key={bar.key} opacity="0.75" width={bar.width} x={bar.x} y={bar.y} />
      ))}
      {geometry.referenceLine != null ? (
        <>
          <line
            x1="4"
            x2="236"
            y1={geometry.referenceLine.y}
            y2={geometry.referenceLine.y}
            stroke="#6f6b7c"
            strokeDasharray="2 2"
            strokeWidth="0.8"
          />
          <text x="198" y={geometry.referenceLine.labelY} fill="#f2f2f6" fontSize="8" fontWeight="700">
            {geometry.referenceLine.label}
          </text>
        </>
      ) : null}
      {geometry.referenceLine != null ? (
        <>
          <polyline
            clipPath={`url(#${aboveReferenceClipId})`}
            data-reference-zone="above"
            fill="none"
            points={geometry.linePoints}
            stroke="#00d084"
            strokeWidth="1.4"
          />
          <polyline
            clipPath={`url(#${belowReferenceClipId})`}
            data-reference-zone="below"
            fill="none"
            points={geometry.linePoints}
            stroke="#ff1f46"
            strokeWidth="1.4"
          />
        </>
      ) : (
        <polyline
          data-reference-zone="fallback"
          fill="none"
          points={geometry.linePoints}
          stroke={fallbackStroke}
          strokeWidth="1.4"
        />
      )}
    </svg>
  );
}

const miniChart = {
  height: 82,
  left: 4,
  priceHeight: 48,
  priceTop: 6,
  sessionEndHour: 15,
  sessionStartHour: 9,
  volumeBase: 80,
  volumeHeight: 18,
  width: 240,
} as const;

type MiniChartPoint = {
  x: number;
  y: number;
};

type MiniChartVolumeBar = {
  height: number;
  key: string;
  width: number;
  x: number;
  y: number;
};

type MiniChartGridLine = {
  label: string;
  x: number;
};

type MiniChartReferenceLine = {
  label: string;
  labelY: number;
  y: number;
};

export type MiniChartGeometry = {
  gridLines: MiniChartGridLine[];
  linePoints: string;
  points: MiniChartPoint[];
  referenceLine: MiniChartReferenceLine | null;
  volumeBars: MiniChartVolumeBar[];
};

export function createMiniChartGeometry(bars: OhlcBar[], referenceValue: number | null | undefined): MiniChartGeometry | null {
  const sessionBars = filterVietnamMarketSessionBars(bars).filter(
    (bar): bar is OhlcBar & { close: number } => typeof bar.close === 'number' && Number.isFinite(bar.close),
  );

  if (sessionBars.length === 0) {
    return null;
  }

  const sessionRange = createVietnamMarketSessionRange(new Date(sessionBars[0].time));
  const sessionStart = sessionRange.from.getTime();
  const sessionEnd = sessionRange.to.getTime();
  const plotWidth = miniChart.width - miniChart.left * 2;
  const reference = typeof referenceValue === 'number' && Number.isFinite(referenceValue) ? referenceValue : null;
  const closes = sessionBars.map((bar) => bar.close);
  const priceValues = reference == null ? closes : [...closes, reference];
  const min = Math.min(...priceValues);
  const max = Math.max(...priceValues);
  const range = max - min || 1;
  const scaleX = (time: string) => {
    const value = new Date(time).getTime();
    const ratio = (value - sessionStart) / (sessionEnd - sessionStart);

    return miniChart.left + clamp(ratio, 0, 1) * plotWidth;
  };
  const scaleY = (value: number) => miniChart.priceTop + ((max - value) / range) * miniChart.priceHeight;
  const points = sessionBars.map((bar) => ({
    x: scaleX(bar.time),
    y: scaleY(bar.close),
  }));
  const linePoints = points.map((point) => `${point.x},${point.y}`).join(' ');
  const maxVolume = Math.max(...sessionBars.map((bar) => bar.volume ?? 0), 1);
  const volumeBarWidth = 2;
  const volumeBars = sessionBars.map((bar, index) => {
    const height = Math.max(((bar.volume ?? 0) / maxVolume) * miniChart.volumeHeight, 1);
    const x = scaleX(bar.time) - volumeBarWidth / 2;

    return {
      height,
      key: `${bar.time}-${index}`,
      width: volumeBarWidth,
      x: clamp(x, miniChart.left, miniChart.width - miniChart.left - volumeBarWidth),
      y: miniChart.volumeBase - height,
    };
  });
  const gridLines = createSessionGridLines();
  const referenceLine = reference == null ? null : {
    label: formatIndexValue(reference),
    labelY: clamp(scaleY(reference) - 4, 10, 55),
    y: scaleY(reference),
  };

  return { gridLines, linePoints, points, referenceLine, volumeBars };
}

function createSessionGridLines() {
  const plotWidth = miniChart.width - miniChart.left * 2;
  const totalHours = miniChart.sessionEndHour - miniChart.sessionStartHour;

  return Array.from({ length: totalHours + 1 }, (_, index) => {
    const hour = miniChart.sessionStartHour + index;

    return {
      label: `${hour.toString().padStart(2, '0')}h`,
      x: miniChart.left + (index / totalHours) * plotWidth,
    };
  });
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

function mergeIndexUpdate(indices: MarketIndex[], update: MarketIndexUpdate) {
  const current = new Map(indices.map((index) => [index.indexName, index]));
  current.set(update.indexName, { ...(current.get(update.indexName) ?? createEmptyIndex(update.indexName)), ...update });
  return orderIndices([...current.values()]);
}

function orderIndices(indices: MarketIndex[]) {
  const byName = new Map(indices.map((index) => [index.indexName, index]));
  return defaultMarketIndexNames.map((indexName) => byName.get(indexName) ?? createEmptyIndex(indexName));
}

function createEmptyIndex(indexName: string): MarketIndex {
  return {
    change: null,
    changePercent: null,
    ceilingCount: null,
    downCount: null,
    floorCount: null,
    highValue: null,
    indexName,
    lowValue: null,
    marketId: '',
    noChangeCount: null,
    referenceValue: null,
    totalValue: null,
    totalVolume: null,
    tradingSessionId: '',
    upCount: null,
    updatedAt: '',
    value: null,
  };
}

function classForChange(value: number | null | undefined) {
  if (value == null || value === 0) {
    return 'text-price-ref';
  }

  return value > 0 ? 'text-price-up' : 'text-price-down';
}

function formatIndexValue(value: number | null | undefined) {
  return value == null ? '-' : value.toLocaleString('en-US', { maximumFractionDigits: 2, minimumFractionDigits: 2 });
}

function formatSigned(value: number | null | undefined) {
  if (value == null) {
    return '-';
  }

  return `${value > 0 ? '+' : ''}${formatIndexValue(value)}`;
}

function formatPercent(value: number | null | undefined) {
  if (value == null) {
    return '-';
  }

  return `${value > 0 ? '+' : ''}${value.toFixed(2)}%`;
}

function formatCount(value: number | null | undefined) {
  return value == null ? '-' : value.toString();
}

function formatCompactVolume(value: number | null | undefined) {
  if (value == null) {
    return '-';
  }

  return value.toLocaleString('en-US');
}

function formatMillion(value: number | null | undefined) {
  return value == null ? '-' : (value / 1_000_000).toLocaleString('en-US', { maximumFractionDigits: 3 });
}

function formatBillion(value: number | null | undefined) {
  return value == null ? '-' : (value / 1_000_000_000).toLocaleString('en-US', { maximumFractionDigits: 3 });
}
