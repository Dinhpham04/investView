import { useMemo } from 'react';
import { formatChange, formatPercent, formatPrice, formatQuantity } from '../market-board/marketBoardFormatters';
import { useSymbolDetailQueries, type SymbolDetailSelection } from './useSymbolDetailQueries';
import type { MarketTrade, OhlcBar, SymbolDetail } from '../../shared/types/market';

type SymbolDetailPanelProps = {
  onClose: () => void;
  selection: SymbolDetailSelection | null;
};

export function SymbolDetailPanel({ onClose, selection }: SymbolDetailPanelProps) {
  const { detailQuery, latestTradesQuery, ohlcQuery } = useSymbolDetailQueries(selection);
  const bars = ohlcQuery.data ?? [];
  const trades = latestTradesQuery.data ?? [];
  const detail = detailQuery.data ?? null;

  if (selection == null) {
    return null;
  }

  return (
    <aside className="border-t border-market-border bg-market-surface" data-testid="symbol-detail-panel">
      <div className="flex min-h-10 flex-wrap items-center justify-between gap-3 border-b border-market-border px-3 py-2">
        <div className="min-w-0">
          <p className="text-[11px] font-semibold uppercase text-market-text-muted">Symbol detail</p>
          <h3 className="truncate text-sm font-bold text-market-text">
            {selection.symbol}
            {detail?.displayName ? <span className="ml-2 text-xs font-semibold text-market-text-muted">{detail.displayName}</span> : null}
          </h3>
        </div>
        <button
          className="h-8 border border-market-border bg-market-surface-2 px-3 text-xs font-semibold text-market-text-muted hover:text-market-text"
          type="button"
          onClick={onClose}
        >
          Close
        </button>
      </div>

      {detailQuery.isPending ? <PanelState label="Loading symbol detail" /> : null}
      {detailQuery.isError ? <PanelState label={detailQuery.error.message} tone="error" /> : null}

      {detail ? (
        <div className="grid min-h-0 grid-cols-1 gap-0 lg:grid-cols-[minmax(280px,360px)_minmax(360px,1fr)_minmax(300px,420px)]">
          <SymbolSnapshot detail={detail} />
          <OhlcChart bars={bars} isError={ohlcQuery.isError} isPending={ohlcQuery.isPending} />
          <LatestTradesTable
            isError={latestTradesQuery.isError}
            isPending={latestTradesQuery.isPending}
            referencePrice={detail.referencePrice}
            trades={trades}
          />
        </div>
      ) : null}
    </aside>
  );
}

function SymbolSnapshot({ detail }: { detail: SymbolDetail }) {
  return (
    <section className="border-b border-market-border p-3 lg:border-b-0 lg:border-r">
      <div className="grid grid-cols-3 gap-2 text-[11px]">
        <Metric label="Last" value={formatPrice(detail.lastPrice)} tone={classForPrice(detail.lastPrice, detail)} />
        <Metric label="+/-" value={formatChange(detail.change, detail.referencePrice)} tone={classForChange(detail.change)} />
        <Metric label="+/-%" value={formatPercent(detail.changePercent)} tone={classForChange(detail.change)} />
        <Metric label="TC" value={formatPrice(detail.referencePrice)} tone="text-price-ref" />
        <Metric label="Tran" value={formatPrice(detail.ceilingPrice)} tone="text-price-ceiling" />
        <Metric label="San" value={formatPrice(detail.floorPrice)} tone="text-price-floor" />
        <Metric label="Open" value={formatPrice(detail.openPrice)} tone={classForPrice(detail.openPrice, detail)} />
        <Metric label="High" value={formatPrice(detail.highPrice)} tone={classForPrice(detail.highPrice, detail)} />
        <Metric label="Low" value={formatPrice(detail.lowPrice)} tone={classForPrice(detail.lowPrice, detail)} />
      </div>

      <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-1 text-[11px]">
        <Info label="ISIN" value={detail.isin || '-'} />
        <Info label="Board" value={detail.boardId} />
        <Info label="Market" value={detail.marketId} />
        <Info label="Group" value={detail.securityGroupId || '-'} />
        <Info label="Status" value={detail.tradingStatus || '-'} />
        <Info label="Admin" value={detail.symbolAdminStatus || '-'} />
        <Info label="Method" value={detail.tradingMethodStatus || '-'} />
        <Info label="Sanction" value={detail.tradingSanctionStatus || '-'} />
        <Info label="Total vol" value={formatQuantity(detail.totalVolume)} />
        <Info label="Room" value={formatQuantity(detail.foreignRoom)} />
      </dl>
    </section>
  );
}

function OhlcChart({ bars, isError, isPending }: { bars: OhlcBar[]; isError: boolean; isPending: boolean }) {
  const geometry = useMemo(() => createChartGeometry(bars), [bars]);

  return (
    <section className="border-b border-market-border p-3 lg:border-b-0 lg:border-r">
      <div className="mb-2 flex items-center justify-between">
        <h4 className="text-xs font-bold text-market-text">Intraday chart</h4>
        <span className="text-[11px] text-market-text-muted">{bars.length} bars</span>
      </div>
      {isPending ? <PanelState label="Loading chart" compact /> : null}
      {isError ? <PanelState label="Chart unavailable" compact tone="error" /> : null}
      {!isPending && !isError && geometry == null ? <PanelState label="No chart data" compact /> : null}
      {geometry ? (
        <svg className="h-48 w-full" role="img" aria-label="OHLC chart" viewBox="0 0 520 190">
          <line className="stroke-market-border" x1="0" x2="520" y1="160" y2="160" />
          {geometry.candles.map((candle) => (
            <g key={`${candle.x}-${candle.openY}`}>
              <line className={candle.className} x1={candle.x} x2={candle.x} y1={candle.highY} y2={candle.lowY} />
              <line className={candle.className} strokeWidth="4" x1={candle.x} x2={candle.x} y1={candle.openY} y2={candle.closeY} />
            </g>
          ))}
        </svg>
      ) : null}
    </section>
  );
}

function LatestTradesTable({
  isError,
  isPending,
  referencePrice,
  trades,
}: {
  isError: boolean;
  isPending: boolean;
  referencePrice: number | null;
  trades: MarketTrade[];
}) {
  return (
    <section className="p-3">
      <div className="mb-2 flex items-center justify-between">
        <h4 className="text-xs font-bold text-market-text">Latest trades</h4>
        <span className="text-[11px] text-market-text-muted">{trades.length} rows</span>
      </div>
      {isPending ? <PanelState label="Loading trades" compact /> : null}
      {isError ? <PanelState label="Trades unavailable" compact tone="error" /> : null}
      {!isPending && !isError && trades.length === 0 ? <PanelState label="No trades" compact /> : null}
      {trades.length > 0 ? (
        <div className="max-h-48 overflow-hidden border border-market-border">
          <table className="w-full border-collapse text-right text-[11px] tabular-nums">
            <thead className="bg-market-surface-2 text-market-text-muted">
              <tr>
                <th className="border-b border-r border-market-border px-2 py-1 text-left">Time</th>
                <th className="border-b border-r border-market-border px-2 py-1">Price</th>
                <th className="border-b border-r border-market-border px-2 py-1">+/-</th>
                <th className="border-b border-market-border px-2 py-1">Qty</th>
              </tr>
            </thead>
            <tbody>
              {trades.slice(0, 8).map((trade) => (
                <tr key={`${trade.time}-${trade.price}-${trade.quantity}`}>
                  <td className="border-b border-r border-market-border px-2 py-1 text-left text-market-text-muted">{formatTime(trade.time)}</td>
                  <td className={`border-b border-r border-market-border px-2 py-1 ${classForTrade(trade.price, referencePrice)}`}>
                    {formatPrice(trade.price)}
                  </td>
                  <td className={`border-b border-r border-market-border px-2 py-1 ${classForChange(trade.change)}`}>
                    {formatChange(trade.change, referencePrice)}
                  </td>
                  <td className="border-b border-market-border px-2 py-1 text-market-text">{formatQuantity(trade.quantity)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </section>
  );
}

function Metric({ label, tone, value }: { label: string; tone: string; value: string }) {
  return (
    <div className="border border-market-border bg-market-surface-2 px-2 py-1">
      <div className="text-market-text-muted">{label}</div>
      <div className={`text-sm font-bold tabular-nums ${tone}`}>{value}</div>
    </div>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt className="text-market-text-muted">{label}</dt>
      <dd className="truncate text-right font-semibold text-market-text">{value}</dd>
    </>
  );
}

function PanelState({ compact = false, label, tone = 'muted' }: { compact?: boolean; label: string; tone?: 'muted' | 'error' }) {
  return (
    <div className={`${compact ? 'py-6' : 'py-10'} text-center text-xs font-semibold ${tone === 'error' ? 'text-state-error' : 'text-market-text-muted'}`}>
      {label}
    </div>
  );
}

function createChartGeometry(bars: OhlcBar[]) {
  const validBars = bars.filter(
    (bar) => bar.open != null && bar.high != null && bar.low != null && bar.close != null,
  );

  if (validBars.length === 0) {
    return null;
  }

  const min = Math.min(...validBars.map((bar) => bar.low as number));
  const max = Math.max(...validBars.map((bar) => bar.high as number));
  const range = max - min || 1;
  const chartTop = 14;
  const chartHeight = 146;
  const step = validBars.length === 1 ? 0 : 500 / (validBars.length - 1);

  return {
    candles: validBars.map((bar, index) => {
      const x = 10 + index * step;
      const toY = (value: number) => chartTop + (max - value) / range * chartHeight;
      const isUp = (bar.close as number) >= (bar.open as number);

      return {
        className: isUp ? 'stroke-price-up' : 'stroke-price-down',
        closeY: toY(bar.close as number),
        highY: toY(bar.high as number),
        lowY: toY(bar.low as number),
        openY: toY(bar.open as number),
        x,
      };
    }),
  };
}

function classForPrice(value: number | null | undefined, detail: SymbolDetail) {
  return classForTrade(value, detail.referencePrice, detail.ceilingPrice, detail.floorPrice);
}

function classForTrade(
  value: number | null | undefined,
  referencePrice: number | null | undefined,
  ceilingPrice?: number | null,
  floorPrice?: number | null,
) {
  if (value == null) {
    return 'text-price-neutral';
  }

  if (ceilingPrice != null && value === ceilingPrice) {
    return 'text-price-ceiling';
  }

  if (floorPrice != null && value === floorPrice) {
    return 'text-price-floor';
  }

  if (referencePrice != null && value > referencePrice) {
    return 'text-price-up';
  }

  if (referencePrice != null && value < referencePrice) {
    return 'text-price-down';
  }

  return 'text-price-ref';
}

function classForChange(value: number | null | undefined) {
  if (value == null) {
    return 'text-price-neutral';
  }

  if (value > 0) {
    return 'text-price-up';
  }

  if (value < 0) {
    return 'text-price-down';
  }

  return 'text-price-ref';
}

function formatTime(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return date.toLocaleTimeString('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    timeZone: 'Asia/Ho_Chi_Minh',
  });
}
