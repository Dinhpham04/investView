import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import type { CellClassParams, ColDef, ValueFormatterParams } from 'ag-grid-community';
import { AgGridReact } from 'ag-grid-react';
import {
  formatChange,
  formatPercent,
  formatPrice,
  formatQuantity,
} from '../market-board/marketBoardFormatters';
import { marketBoardTheme } from '../market-board/marketBoardTheme';
import { SymbolPriceChart } from './SymbolPriceChart';
import {
  aggregateOhlcBarsForTimeframe,
  chartTimeframes,
  defaultChartTimeframe,
  mergeOhlcHistoryPages,
  useSymbolDetailQueries,
  type ChartTimeframe,
  type SymbolDetailSelection,
} from './useSymbolDetailQueries';
import type { SymbolOhlcSubscription } from '../../shared/realtime/useQuoteHubConnection';
import type { MarketQuote, MarketTrade, MarketTradeUpdate, OhlcBar, PriceLevel, SymbolDetail } from '../../shared/types/market';

type SymbolDetailPanelProps = {
  liveQuote?: MarketQuote | null;
  liveTrade?: MarketTradeUpdate | null;
  onClose: () => void;
  onOhlcSubscriptionChange?: (subscription: SymbolOhlcSubscription | null) => void;
  selection: SymbolDetailSelection | null;
};

const tabs = ['Giao dịch', 'Hồ sơ', 'Cổ đông', 'Vốn và cổ tức', 'Tin tức', 'Lịch sự kiện', 'Thống kê', 'Tài chính'];

type LatestTradeRow = MarketTrade & {
  id: string;
  sideLabel: string;
};

type PriceBand = Pick<SymbolDetail, 'ceilingPrice' | 'floorPrice' | 'referencePrice'>;

export function SymbolDetailPanel({
  liveQuote = null,
  liveTrade = null,
  onClose,
  onOhlcSubscriptionChange,
  selection,
}: SymbolDetailPanelProps) {
  const [chartTimeframe, setChartTimeframe] = useState<ChartTimeframe>(defaultChartTimeframe);
  const [realtimeTrades, setRealtimeTrades] = useState<MarketTrade[]>([]);
  const lastRealtimeTradeKeyRef = useRef<string | null>(null);
  const { detailQuery, latestTradesQuery, ohlcQuery } = useSymbolDetailQueries(selection, chartTimeframe);
  const bars = useMemo(() => mergeOhlcHistoryPages(ohlcQuery.data?.pages), [ohlcQuery.data?.pages]);
  const liveBars = useMemo(() => mergeLiveQuoteIntoOhlcBars(bars ?? [], liveQuote, chartTimeframe), [bars, chartTimeframe, liveQuote]);
  const chartBars = useMemo(() => aggregateOhlcBarsForTimeframe(liveBars, chartTimeframe), [liveBars, chartTimeframe]);
  const detail = useMemo(() => mergeSymbolDetailWithQuote(detailQuery.data ?? null, liveQuote), [detailQuery.data, liveQuote]);
  const trades = useMemo(
    () => mergeLatestTrades(realtimeTrades, latestTradesQuery.data ?? []),
    [latestTradesQuery.data, realtimeTrades],
  );

  useEffect(() => {
    if (selection == null) {
      onOhlcSubscriptionChange?.(null);
      return undefined;
    }

    onOhlcSubscriptionChange?.({
      resolutions: [chartTimeframe.resolution],
      symbol: selection.symbol,
    });

    return () => {
      onOhlcSubscriptionChange?.(null);
    };
  }, [chartTimeframe.resolution, onOhlcSubscriptionChange, selection]);

  useEffect(() => {
    lastRealtimeTradeKeyRef.current = null;
    setRealtimeTrades([]);
  }, [selection?.boardId, selection?.symbol]);

  useEffect(() => {
    if (selection == null || liveTrade == null || liveTrade.boardId !== selection.boardId || liveTrade.symbol !== selection.symbol) {
      return;
    }

    const nextTrade = normalizeRealtimeTrade(liveTrade, detail?.referencePrice ?? liveQuote?.referencePrice);
    const tradeKey = getTradeKey(nextTrade);
    if (lastRealtimeTradeKeyRef.current === tradeKey) {
      return;
    }

    lastRealtimeTradeKeyRef.current = tradeKey;
    setRealtimeTrades((currentTrades) => [nextTrade, ...currentTrades].slice(0, 30));
  }, [detail?.referencePrice, liveQuote?.referencePrice, liveTrade, selection]);

  if (selection == null) {
    return null;
  }

  return (
    <aside
      className="fixed inset-x-3 top-[calc(50%-12px)] z-150 flex h-[min(760px,calc(100vh-96px))] min-h-0 -translate-y-1/2 flex-col overflow-hidden border border-[#363341] bg-[#1c1928] text-white shadow-2xl"
      data-testid="symbol-detail-panel"
    >
      <SymbolOverlayHeader
        detail={detail}
        isError={detailQuery.isError}
        isPending={detailQuery.isPending}
        onClose={onClose}
        selection={selection}
      />

      <nav className="flex h-10 shrink-0 items-end gap-5 border-b border-[#34313d] px-3 text-sm font-semibold text-white">
        {tabs.map((tab) => (
          <button
            key={tab}
            className={`h-full border-b-2 px-1 ${tab === 'Giao dịch' ? 'border-[#d51f37] text-white' : 'border-transparent text-[#c8c6d4] hover:text-white'}`}
            type="button"
          >
            {tab}
          </button>
        ))}
      </nav>

      {detail ? (
        <div className="grid min-h-0 flex-1 grid-cols-1 bg-black lg:grid-cols-[minmax(560px,1fr)_minmax(340px,360px)_minmax(330px,360px)]">
          <SymbolPriceChart
            bars={chartBars}
            canLoadMoreHistory={Boolean(ohlcQuery.hasNextPage && !ohlcQuery.isFetchingNextPage)}
            detail={detail}
            isError={ohlcQuery.isError}
            isLoadingMoreHistory={ohlcQuery.isFetchingNextPage}
            isPending={ohlcQuery.isPending}
            onLoadMoreHistory={() => {
              void ohlcQuery.fetchNextPage();
            }}
            onTimeframeChange={setChartTimeframe}
            selectedTimeframe={chartTimeframe}
            timeframes={chartTimeframes}
          />
          <MarketDepthPanel detail={detail} />
          <LatestTradesPanel detail={detail} isError={latestTradesQuery.isError} isPending={latestTradesQuery.isPending} trades={trades} />
        </div>
      ) : (
        <PanelState label={detailQuery.isError ? detailQuery.error.message : 'Đang tải thông tin mã'} tone={detailQuery.isError ? 'error' : 'muted'} />
      )}
    </aside>
  );
}

function SymbolOverlayHeader({
  detail,
  isError,
  isPending,
  onClose,
  selection,
}: {
  detail: SymbolDetail | null;
  isError: boolean;
  isPending: boolean;
  onClose: () => void;
  selection: SymbolDetailSelection;
}) {
  const displayName = detail?.name || detail?.displayName || '';
  const priceTone = detail == null ? 'text-[#c8c6d4]' : classForPrice(detail.lastPrice, detail);

  return (
    <header className="shrink-0 bg-[#1d1a2a] px-3 pb-3 pt-4">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0 flex-1">
          <div className="mb-2 flex min-w-0 items-center gap-2">
            <h3 className="truncate text-xl font-extrabold tracking-wide text-white">
              {selection.symbol}
              {detail ? <span className="ml-4 text-sm font-bold">({detail.marketId}) {displayName}</span> : null}
            </h3>
            {isPending ? <span className="text-xs font-semibold text-[#b8b5c7]">Đang tải</span> : null}
            {isError ? <span className="text-xs font-semibold text-state-error">Lỗi dữ liệu</span> : null}
          </div>

          <div className="grid min-w-0 grid-cols-1 gap-x-6 gap-y-2 lg:grid-cols-[280px_minmax(200px,1fr)_minmax(360px,430px)] lg:grid-rows-[36px_20px]">
            <div className="flex min-w-0 items-start gap-4 tabular-nums lg:col-start-1 lg:row-start-1">
              <div className={`text-4xl font-light leading-none ${priceTone}`}>{formatPrice(detail?.lastPrice)}</div>
              <div className={`text-sm leading-[18px] ${classForChange(detail?.change)}`}>
                <div>{formatChange(detail?.change, detail?.referencePrice)}</div>
                <div>{formatPercent(detail?.changePercent)}</div>
              </div>
            </div>

            <div className="text-sm leading-5 lg:col-start-1 lg:row-start-2">
              <span className="text-[#c8c6d4]">MỞ CỬA/Trung bình:</span>
              <span className={`ml-2 ${detail ? classForPrice(detail.openPrice, detail) : 'text-[#c8c6d4]'}`}>{formatPrice(detail?.openPrice)}</span>
              <span className={`ml-1 ${detail ? classForPrice(detail.lastPrice, detail) : 'text-[#c8c6d4]'}`}>/{formatPrice(detail?.lastPrice)}</span>
            </div>

            <div className="text-sm leading-5 lg:col-start-2 lg:row-start-2">
              <span className="text-[#c8c6d4]">Thấp/Cao:</span>
              <span className={`ml-3 ${detail ? classForPrice(detail.lowPrice, detail) : 'text-[#c8c6d4]'}`}>{formatPrice(detail?.lowPrice)}</span>
              <span className={`ml-1 ${detail ? classForPrice(detail.highPrice, detail) : 'text-[#c8c6d4]'}`}>/{formatPrice(detail?.highPrice)}</span>
            </div>

            <div className="grid grid-cols-[repeat(3,minmax(76px,1fr))] grid-rows-[36px_20px] gap-x-4 gap-y-2 text-center text-sm lg:col-start-3 lg:row-span-2 lg:row-start-1">
              <HeaderMetric label="Trần" value={formatPrice(detail?.ceilingPrice)} valueClass="text-price-ceiling" />
              <HeaderMetric label="Sàn" value={formatPrice(detail?.floorPrice)} valueClass="text-price-floor" />
              <HeaderMetric label="Tham chiếu" value={formatPrice(detail?.referencePrice)} valueClass="text-price-ref" />
              <div className="col-span-3 flex items-center justify-end gap-7 text-left leading-5">
                <span className="text-[#c8c6d4]">TỔNG KL:</span>
                <span className="min-w-[96px] text-right tabular-nums text-white">{formatQuantity(detail?.totalVolume)}</span>
              </div>
            </div>
          </div>
        </div>

        <div className="flex shrink-0 items-start gap-2">
          <button className="h-9 bg-[#16a77e] px-8 text-sm font-bold rounded-sm text-white hover:bg-[#1db98e]" type="button">
            Đặt lệnh
          </button>
          <button className="grid size-9 place-items-center text-3xl leading-none text-[#c8c6d4] hover:text-white" type="button" onClick={onClose} aria-label="Đóng">
            X
          </button>
        </div>
      </div>
    </header>
  );
}

function HeaderMetric({ label, value, valueClass }: { label: string; value: string; valueClass: string }) {
  return (
    <div className="leading-4 flex flex-col items-end">
      <div className="text-[#f2f2f6]">{label}</div>
      <div className={`mt-1 tabular-nums ${valueClass}`}>{value}</div>
    </div>
  );
}

function MarketDepthPanel({ detail }: { detail: SymbolDetail }) {
  const bidLevels = normalizeLevels(detail.bidLevels);
  const askLevels = normalizeLevels(detail.askLevels);
  const bidTotal = sumQuantities(bidLevels);
  const askTotal = sumQuantities(askLevels);
  const totalDepth = bidTotal + askTotal;
  const bidPercent = totalDepth > 0 ? bidTotal / totalDepth * 100 : 50;
  const tableMaxQuantity = Math.max(
    ...bidLevels.slice(0, 3).map((level) => level.quantity ?? 0),
    ...askLevels.slice(0, 3).map((level) => level.quantity ?? 0),
    1,
  );

  return (
    <section className="flex min-h-0 flex-col border-r border-[#0b0b13] bg-[#1d1a2a]">
      <PanelTitle title="Độ sâu thị trường" />
      <div className="grid h-7 grid-cols-4 items-center gap-x-1 bg-[#403b4d] px-2 text-xs font-medium text-[#d7d4e3]">
        <span className="text-left">KL</span>
        <span className="text-right pr-2">Giá mua</span>
        <span className="text-left pl-2">Giá bán</span>
        <span className="text-right">KL</span>
      </div>

      <div className="text-sm font-semibold tabular-nums">
        {[0, 1, 2].map((index) => {
          const bid = bidLevels[index];
          const ask = askLevels[index];
          const bidWidth = `${Math.min(((bid?.quantity ?? 0) / tableMaxQuantity) * 100, 100)}%`;
          const askWidth = `${Math.min(((ask?.quantity ?? 0) / tableMaxQuantity) * 100, 100)}%`;

          return (
            <div key={index} className="grid h-8 grid-cols-4 items-center gap-x-1 px-2 odd:bg-[#252238]">
              <span className="text-left text-[#eef2ff]">{formatQuantity(bid?.quantity)}</span>

              <span className="relative flex h-full items-center justify-end pr-2">
                <span className="absolute inset-y-1 right-0 bg-[#322d46]" style={{ width: bidWidth }} />
                <span className={`relative z-10 ${classForPrice(bid?.price, detail)}`}>{formatPrice(bid?.price)}</span>
              </span>

              <span className="relative flex h-full items-center justify-start pl-2">
                <span className="absolute inset-y-1 left-0 bg-[#322d46]" style={{ width: askWidth }} />
                <span className={`relative z-10 ${classForPrice(ask?.price, detail)}`}>{formatPrice(ask?.price)}</span>
              </span>

              <span className="text-right text-[#eef2ff]">{formatQuantity(ask?.quantity)}</span>
            </div>
          );
        })}
      </div>

      <div className="px-0">
        <div className="flex h-1.5 bg-[#191724]">
          <div className="bg-[#00b98a]" style={{ width: `${bidPercent}%` }} />
          <div className="bg-[#e01f3b]" style={{ width: `${100 - bidPercent}%` }} />
        </div>
        <div className="flex h-[52px] items-center justify-between px-2 text-sm">
          <span>Dư mua: {formatQuantity(bidTotal)}</span>
          <span>Dư bán: {formatQuantity(askTotal)}</span>
        </div>
      </div>

      <MarketDepthChart bids={bidLevels} asks={askLevels} detail={detail} />
    </section>
  );
}

function MarketDepthChart({ asks, bids, detail }: { asks: PriceLevel[]; bids: PriceLevel[]; detail: SymbolDetail }) {
  const columns = useMemo(() => {
    let bidCumulative = 0;
    const bidColumnsOriginal = bids.map((level) => {
      bidCumulative += level.quantity ?? 0;
      return { ...level, side: 'bid' as const, cumulativeQuantity: bidCumulative };
    });

    let askCumulative = 0;
    const askColumns = asks.map((level) => {
      askCumulative += level.quantity ?? 0;
      return { ...level, side: 'ask' as const, cumulativeQuantity: askCumulative };
    });

    const allLevels = [
      ...bidColumnsOriginal.reverse(),
      ...askColumns,
    ];
    const maxQuantity = Math.max(...allLevels.map((level) => level.quantity ?? 0), 1);

    return allLevels.map((level) => ({
      ...level,
      height: Math.max(((level.quantity ?? 0) / maxQuantity) * 100, 4),
    }));
  }, [asks, bids]);
  const maxQuantity = Math.max(...columns.map((level) => level.quantity ?? 0), 1);
  const axisSteps = [1, 0.75, 0.5, 0.25, 0];

  return (
    <div className="mt-1 flex min-h-0 flex-1 flex-col border-t-4 border-black bg-[#1d1a2a]">
      <PanelTitle title="Biểu đồ độ sâu thị trường" />
      <div className="relative min-h-[220px] flex-1 px-4 pb-8 pl-11 pr-5 pt-6">
        <div className="absolute bottom-8 left-10 top-6 w-px bg-[#555162]" />
        <div className="absolute bottom-8 left-10 right-5 h-px bg-[#555162]" />
        {axisSteps.map((step) => (
          <span
            key={step}
            className="absolute left-1 w-8 -translate-y-1/2 text-right text-[11px] tabular-nums text-[#7f7a8f]"
            style={{ top: `calc(1.5rem + ${(1 - step) * 100}% - ${(1 - step) * 3.5}rem)` }}
          >
            {formatDepthAxisQuantity(maxQuantity * step)}
          </span>
        ))}
        <div className="relative flex h-full items-end gap-0">
          {columns.map((level, index) => (
            <div key={`${level.side}-${level.price}-${index}`} className="group relative flex h-full min-w-0 flex-1 flex-col justify-end">
              <div
                className={`relative ${level.side === 'bid' ? 'bg-[#3f9476]/95' : 'bg-[#a23631]/95'}`}
                style={{ height: `${level.height}%` }}
              >
                <div className="pointer-events-none absolute bottom-full left-1/2 z-[100] mb-1 hidden -translate-x-1/2 flex-col rounded border border-[#3e3a4f] bg-[#1a1726] p-2 text-xs leading-[1.4] shadow-xl group-hover:flex whitespace-nowrap">
                  <span className="font-semibold text-[#eef2ff]">{level.side === 'bid' ? 'Bên mua' : 'Bên bán'}</span>
                  <span className="mt-1 text-[#a19db1]">
                    Giá: <span className={classForPrice(level.price, detail)}>{formatPrice(level.price)}</span>
                  </span>
                  <span className="text-[#a19db1]">
                    KL tích lũy: <span className="text-[#5ca7de]">{formatQuantity(level.cumulativeQuantity)}</span>
                  </span>
                </div>
              </div>
              <div className={`absolute -bottom-6 left-0 right-0 text-center text-[10px] tabular-nums ${classForPrice(level.price, detail)}`}>
                {formatPrice(level.price)}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function LatestTradesPanel({
  detail,
  isError,
  isPending,
  trades,
}: {
  detail: SymbolDetail;
  isError: boolean;
  isPending: boolean;
  trades: MarketTrade[];
}) {
  const totals = useMemo(() => summarizeTradeSides(trades), [trades]);
  const rowData = useMemo(() => mapTradesToRows(trades), [trades]);
  const priceBand = useMemo(
    () => ({
      ceilingPrice: detail.ceilingPrice,
      floorPrice: detail.floorPrice,
      referencePrice: detail.referencePrice,
    }),
    [detail.ceilingPrice, detail.floorPrice, detail.referencePrice],
  );
  const columnDefs = useMemo(() => createLatestTradeColumnDefs(priceBand), [priceBand]);

  return (
    <section className="flex min-h-0 flex-col bg-[#1d1a2a]">
      <PanelTitle
        title="Khớp lệnh"
        aside={
          <button className="grid size-5 place-items-center border border-[#777589] text-xs leading-4 text-[#d7d4e3]" type="button" title="Mở rộng khớp lệnh">
            ▤
          </button>
        }
      />
      <div className="grid h-9 grid-cols-3 items-center gap-2 border-b border-[#32303d] px-2 text-sm tabular-nums">
        <span>KL: {formatCompactQuantity(detail.totalVolume)}</span>
        <span className="text-price-up">M: {formatCompactQuantity(totals.buy)}</span>
        <span className="text-price-down">B: {formatCompactQuantity(totals.sell)}</span>
      </div>
      {isPending ? <PanelState label="Đang tải khớp lệnh" compact /> : null}
      {isError ? <PanelState label="Không tải được khớp lệnh" compact tone="error" /> : null}
      {!isPending && !isError && trades.length === 0 ? <PanelState label="Không có dữ liệu khớp lệnh" compact /> : null}
      {!isPending && !isError && trades.length > 0 ? (
        <div className="latest-trades-grid min-h-0 flex-1">
          <AgGridReact<LatestTradeRow>
            columnDefs={columnDefs}
            defaultColDef={defaultLatestTradeColumnDef}
            getRowId={(params) => params.data.id}
            headerHeight={27}
            rowData={rowData}
            rowHeight={32}
            suppressCellFocus
            suppressHorizontalScroll
            suppressMovableColumns
            theme={marketBoardTheme}
          />
        </div>
      ) : null}
    </section>
  );
}

const defaultLatestTradeColumnDef: ColDef<LatestTradeRow> = {
  cellClass: 'latest-trade-cell latest-trade-cell--number',
  resizable: false,
  sortable: true,
  suppressMovable: true,
};

function createLatestTradeColumnDefs(priceBand: PriceBand): ColDef<LatestTradeRow>[] {
  return [
    {
      cellClass: 'latest-trade-cell latest-trade-cell--time',
      flex: 1.15,
      field: 'time',
      headerClass: 'latest-trade-header-left',
      headerName: 'Thời gian',
      minWidth: 62,
      sort: 'desc',
      valueFormatter: (params) => formatTime(params.value as string),
    },
    {
      flex: 0.78,
      field: 'quantity',
      headerName: 'KL',
      minWidth: 46,
      valueFormatter: formatQuantityValue,
    },
    {
      cellClass: (params) => latestTradePriceCellClass(params, priceBand),
      flex: 0.72,
      field: 'price',
      headerName: 'Giá',
      minWidth: 46,
      valueFormatter: formatPriceValue,
    },
    {
      cellClass: (params) => latestTradeChangeCellClass(params.value as number | null | undefined),
      flex: 0.76,
      field: 'change',
      headerName: '+/-',
      minWidth: 48,
      valueFormatter: (params) => formatChange(params.value as number | null | undefined, priceBand.referencePrice),
    },
    {
      cellClass: (params) => latestTradeChangeCellClass(params.value as number | null | undefined),
      flex: 0.9,
      field: 'changePercent',
      headerName: '+/ (%)',
      minWidth: 54,
      valueFormatter: (params) => formatPercent(params.value as number | null | undefined),
    },
    {
      cellClass: (params) => latestTradeSideCellClass(params.value as string),
      flex: 0.5,
      field: 'sideLabel',
      headerName: 'M/B',
      maxWidth: 42,
      minWidth: 34,
    },
  ];
}

function mapTradesToRows(trades: MarketTrade[]): LatestTradeRow[] {
  return trades.map((trade) => ({
    ...trade,
    id: `${trade.time}:${trade.price ?? ''}:${trade.quantity ?? ''}:${trade.side}`,
    sideLabel: normalizeTradeSide(trade.side),
  }));
}

function formatQuantityValue(params: ValueFormatterParams<LatestTradeRow, number | null | undefined>) {
  return formatQuantity(params.value);
}

function formatPriceValue(params: ValueFormatterParams<LatestTradeRow, number | null | undefined>) {
  return formatPrice(params.value);
}

function latestTradePriceCellClass(params: CellClassParams<LatestTradeRow>, priceBand: PriceBand) {
  return `latest-trade-cell latest-trade-cell--number ${quotePriceClassForPrice(params.value as number | null | undefined, priceBand)}`;
}

function latestTradeChangeCellClass(value: number | null | undefined) {
  return `latest-trade-cell latest-trade-cell--number ${quotePriceClassForChange(value)}`;
}

function latestTradeSideCellClass(value: string) {
  if (value === 'B') {
    return 'latest-trade-cell latest-trade-cell--number quote-price-up';
  }

  if (value === 'S') {
    return 'latest-trade-cell latest-trade-cell--number quote-price-down';
  }

  return 'latest-trade-cell latest-trade-cell--number latest-trade-cell--side-neutral';
}

function quotePriceClassForPrice(value: number | null | undefined, detail: PriceBand) {
  if (value == null) {
    return 'quote-price-neutral';
  }

  if (detail.ceilingPrice != null && value === detail.ceilingPrice) {
    return 'quote-price-ceiling';
  }

  if (detail.floorPrice != null && value === detail.floorPrice) {
    return 'quote-price-floor';
  }

  if (detail.referencePrice != null && value > detail.referencePrice) {
    return 'quote-price-up';
  }

  if (detail.referencePrice != null && value < detail.referencePrice) {
    return 'quote-price-down';
  }

  return 'quote-price-reference';
}

function quotePriceClassForChange(value: number | null | undefined) {
  if (value == null) {
    return 'quote-price-neutral';
  }

  if (value > 0) {
    return 'quote-price-up';
  }

  if (value < 0) {
    return 'quote-price-down';
  }

  return 'quote-price-reference';
}

function PanelTitle({ aside, title }: { aside?: ReactNode; title: string }) {
  return (
    <div className="flex h-[31px] shrink-0 items-center justify-center border-b border-[#32303d] bg-[#1d1a2a] px-2 text-sm font-medium text-white">
      <span className="flex-1 text-center">{title}</span>
      {aside ? <span className="ml-auto">{aside}</span> : null}
    </div>
  );
}

function PanelState({ compact = false, label, tone = 'muted' }: { compact?: boolean; label: string; tone?: 'muted' | 'error' }) {
  return (
    <div className={`${compact ? 'py-6' : 'grid flex-1 place-items-center py-10'} text-center text-xs font-semibold ${tone === 'error' ? 'text-state-error' : 'text-market-text-muted'}`}>
      {label}
    </div>
  );
}

function mergeSymbolDetailWithQuote(detail: SymbolDetail | null, liveQuote: MarketQuote | null): SymbolDetail | null {
  if (detail == null || liveQuote == null || detail.boardId !== liveQuote.boardId || detail.symbol !== liveQuote.symbol) {
    return detail;
  }

  return {
    ...detail,
    ...liveQuote,
    displayName: liveQuote.displayName || detail.displayName,
    name: detail.name,
  };
}

function mergeLatestTrades(realtimeTrades: MarketTrade[], snapshotTrades: MarketTrade[]) {
  const seen = new Set<string>();
  const mergedTrades: MarketTrade[] = [];

  for (const trade of [...realtimeTrades, ...snapshotTrades]) {
    const key = getTradeKey(trade);
    if (seen.has(key)) {
      continue;
    }

    seen.add(key);
    mergedTrades.push(trade);
  }

  return mergedTrades.slice(0, 30);
}

function normalizeRealtimeTrade(update: MarketTradeUpdate, referencePrice: number | null | undefined): MarketTrade {
  const change = update.change ?? deriveTradeChange(update.price, referencePrice);

  return {
    ...update,
    change,
    changePercent: update.changePercent ?? deriveTradeChangePercent(change, referencePrice),
  };
}

function deriveTradeChange(price: number | null | undefined, referencePrice: number | null | undefined) {
  if (price == null || referencePrice == null) {
    return null;
  }

  return normalizePriceForReference(price, referencePrice) - referencePrice;
}

function deriveTradeChangePercent(change: number | null | undefined, referencePrice: number | null | undefined) {
  if (change == null || referencePrice == null || referencePrice === 0) {
    return null;
  }

  return change / referencePrice * 100;
}

function normalizePriceForReference(price: number, referencePrice: number) {
  if (Math.abs(referencePrice) >= 1000 && Math.abs(price) < 1000) {
    return price * 1000;
  }

  if (Math.abs(referencePrice) < 1000 && Math.abs(price) >= 1000) {
    return price / 1000;
  }

  return price;
}

function getTradeKey(trade: MarketTrade) {
  return `${trade.time}:${trade.price ?? ''}:${trade.quantity ?? ''}:${trade.side}`;
}

function mergeLiveQuoteIntoOhlcBars(bars: OhlcBar[], liveQuote: MarketQuote | null, timeframe: ChartTimeframe): OhlcBar[] {
  if (liveQuote == null || liveQuote.lastPrice == null) {
    return bars;
  }

  const quoteTime = new Date(liveQuote.updatedAt).getTime();
  const bucketTime = floorBarTime(quoteTime, timeframe.resolution);
  if (Number.isNaN(quoteTime) || bucketTime == null) {
    return bars;
  }

  const nextBars = [...bars];
  const matchingIndex = findLatestMatchingBucketIndex(nextBars, bucketTime, timeframe.resolution);
  const existingBar = matchingIndex >= 0 ? nextBars[matchingIndex] : null;
  const mergedBar = createLiveOhlcBar(existingBar, liveQuote, timeframe, bucketTime);

  if (matchingIndex >= 0) {
    nextBars[matchingIndex] = mergedBar;
  } else {
    nextBars.push(mergedBar);
  }

  return nextBars.sort((left, right) => new Date(left.time).getTime() - new Date(right.time).getTime());
}

function findLatestMatchingBucketIndex(bars: OhlcBar[], bucketTime: number, resolution: string) {
  for (let index = bars.length - 1; index >= 0; index -= 1) {
    const barTime = new Date(bars[index].time).getTime();
    if (floorBarTime(barTime, resolution) === bucketTime) {
      return index;
    }
  }

  return -1;
}

function createLiveOhlcBar(existingBar: OhlcBar | null, liveQuote: MarketQuote, timeframe: ChartTimeframe, bucketTime: number): OhlcBar {
  const price = liveQuote.lastPrice as number;
  const open = existingBar?.open ?? liveQuote.openPrice ?? price;

  return {
    close: price,
    high: maxNumber(existingBar?.high, liveQuote.highPrice, price),
    low: minNumber(existingBar?.low, liveQuote.lowPrice, price),
    open,
    resolution: existingBar?.resolution ?? timeframe.resolution,
    symbol: existingBar?.symbol ?? liveQuote.symbol,
    time: existingBar?.time ?? new Date(bucketTime).toISOString(),
    volume: getLiveBarVolume(existingBar, liveQuote, timeframe),
  };
}

function getLiveBarVolume(existingBar: OhlcBar | null, liveQuote: MarketQuote, timeframe: ChartTimeframe) {
  if (timeframe.resolution === '1D' || timeframe.resolution === '1W') {
    return liveQuote.totalVolume ?? existingBar?.volume ?? liveQuote.lastQuantity;
  }

  return existingBar?.volume ?? liveQuote.lastQuantity;
}

function floorBarTime(time: number, resolution: string) {
  if (Number.isNaN(time)) {
    return null;
  }

  const resolutionMs = parseResolutionMs(resolution);
  if (resolutionMs == null) {
    return null;
  }

  return Math.floor(time / resolutionMs) * resolutionMs;
}

function parseResolutionMs(resolution: string) {
  if (resolution.endsWith('H')) {
    return Number.parseInt(resolution, 10) * 60 * 60 * 1000;
  }

  if (resolution === '1D') {
    return 24 * 60 * 60 * 1000;
  }

  if (resolution === '1W') {
    return 7 * 24 * 60 * 60 * 1000;
  }

  const minutes = Number.parseInt(resolution, 10);
  return Number.isFinite(minutes) ? minutes * 60 * 1000 : null;
}

function maxNumber(...values: Array<number | null | undefined>) {
  const validValues = values.filter((value): value is number => value != null);
  return validValues.length === 0 ? null : Math.max(...validValues);
}

function minNumber(...values: Array<number | null | undefined>) {
  const validValues = values.filter((value): value is number => value != null);
  return validValues.length === 0 ? null : Math.min(...validValues);
}

function normalizeLevels(levels: PriceLevel[]) {
  return levels
    .filter((level) => level.price != null || level.quantity != null)
    .slice(0, 3);
}

function sumQuantities(levels: PriceLevel[]) {
  return levels.reduce((total, level) => total + (level.quantity ?? 0), 0);
}

function summarizeTradeSides(trades: MarketTrade[]) {
  return trades.reduce(
    (summary, trade) => {
      const side = normalizeTradeSide(trade.side);
      const quantity = trade.quantity ?? 0;
      if (side === 'B') {
        summary.buy += quantity;
      } else if (side === 'S') {
        summary.sell += quantity;
      }

      return summary;
    },
    { buy: 0, sell: 0 },
  );
}

function normalizeTradeSide(side: string) {
  const normalized = side.trim().toUpperCase();
  if (normalized === 'B' || normalized === 'BUY' || normalized === '1') {
    return 'B';
  }

  if (normalized === 'S' || normalized === 'SELL' || normalized === '2') {
    return 'S';
  }

  return normalized;
}

function classForPrice(value: number | null | undefined, detail: PriceBand) {
  if (value == null) {
    return 'text-price-neutral';
  }

  if (detail.ceilingPrice != null && value === detail.ceilingPrice) {
    return 'text-price-ceiling';
  }

  if (detail.floorPrice != null && value === detail.floorPrice) {
    return 'text-price-floor';
  }

  if (detail.referencePrice != null && value > detail.referencePrice) {
    return 'text-price-up';
  }

  if (detail.referencePrice != null && value < detail.referencePrice) {
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

function formatCompactQuantity(value: number | null | undefined) {
  if (value == null) {
    return '-';
  }

  if (value >= 1_000_000) {
    return `${(value / 1_000_000).toFixed(1)}M`;
  }

  if (value >= 1_000) {
    return `${(value / 1_000).toFixed(1)}K`;
  }

  return formatQuantity(value);
}

function formatDepthAxisQuantity(value: number) {
  if (value >= 1_000) {
    return `${Math.round(value / 1_000)}K`;
  }

  return Math.round(value).toString();
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
