import { useCallback, useDeferredValue, useEffect, useMemo, useRef, useState } from 'react';
import type { GridApi, GridReadyEvent, RowClickedEvent } from 'ag-grid-community';
import { AgGridReact } from 'ag-grid-react';
import { useMarketQuotesQuery } from './useMarketQuotesQuery';
import { mapQuoteToMarketBoardRow, type MarketBoardFlashClasses, type MarketBoardRow } from './marketBoardFormatters';
import { defaultMarketBoardColumnDef, defaultMarketBoardColumnGroupDef, marketBoardColumnDefs } from './marketBoardColumns';
import { marketBoardTheme } from './marketBoardTheme';
import { systemExchangeLists, systemIndexLists, type SystemMarketList } from './marketLists';
import { applyQuoteUpdate } from './marketBoardRealtime';
import { MarketIndexOverview } from '../market-index/MarketIndexOverview';
import { OrderTicketPanel } from '../order-ticket/OrderTicketPanel';
import { SymbolDetailPanel } from '../symbol-detail/SymbolDetailPanel';
import { WatchlistPanel } from '../watchlist/WatchlistPanel';
import type { SymbolDetailSelection } from '../symbol-detail/useSymbolDetailQueries';
import { useQuoteHubConnection } from '../../shared/realtime/useQuoteHubConnection';
import type { MarketIndexUpdate, MarketQuote, MarketQuoteUpdate, MarketTradeUpdate, QuoteStreamStatus } from '../../shared/types/market';

type ActiveMarketFilter =
  | { kind: 'exchange'; list: SystemMarketList }
  | { kind: 'index'; list: SystemMarketList };

export function MarketBoard() {
  const gridApiRef = useRef<GridApi<MarketBoardRow> | null>(null);
  const quotesRef = useRef<MarketQuote[]>([]);
  const flashClassesByRowRef = useRef<Record<string, MarketBoardFlashClasses>>({});
  const flashClearTimersRef = useRef<Record<string, number>>({});
  const [selectedIndexCode, setSelectedIndexCode] = useState('VN30');
  const [activeFilter, setActiveFilter] = useState<ActiveMarketFilter>({
    kind: 'index',
    list: systemIndexLists.find((marketList) => marketList.code === 'VN30') ?? systemIndexLists[0],
  });
  const [symbolSearch, setSymbolSearch] = useState('');
  const [quotes, setQuotes] = useState<MarketQuote[]>([]);
  const [flashClassesByRow, setFlashClassesByRow] = useState<Record<string, MarketBoardFlashClasses>>({});
  const [selectedSymbol, setSelectedSymbol] = useState<SymbolDetailSelection | null>(null);
  const [latestMarketIndexUpdate, setLatestMarketIndexUpdate] = useState<MarketIndexUpdate | null>(null);
  const [latestTradeUpdate, setLatestTradeUpdate] = useState<MarketTradeUpdate | null>(null);
  const [streamStatus, setStreamStatus] = useState<QuoteStreamStatus | null>(null);
  const deferredSymbolSearch = useDeferredValue(symbolSearch);
  const quotesQueryParams = useMemo(
    () => ({
      boardId: 'G1',
      marketId: activeFilter.kind === 'exchange' ? activeFilter.list.dnseMarketId : undefined,
      indexName: activeFilter.kind === 'index' ? activeFilter.list.dnseIndexName : undefined,
    }),
    [activeFilter],
  );
  const quotesQuery = useMarketQuotesQuery(quotesQueryParams);
  const marketBoardSubscription = useMemo(
    () => ({
      boardId: quotesQueryParams.boardId,
      symbols: quotes.map((quote) => quote.symbol),
    }),
    [quotes, quotesQueryParams.boardId],
  );
  const scheduleFlashClear = useCallback((rowId: string) => {
    const existingTimer = flashClearTimersRef.current[rowId];
    if (existingTimer != null) {
      window.clearTimeout(existingTimer);
    }

    flashClearTimersRef.current[rowId] = window.setTimeout(() => {
      const nextFlashClassesByRow = { ...flashClassesByRowRef.current };
      delete nextFlashClassesByRow[rowId];
      delete flashClearTimersRef.current[rowId];
      flashClassesByRowRef.current = nextFlashClassesByRow;
      setFlashClassesByRow(nextFlashClassesByRow);

      const quote = quotesRef.current.find((item) => getRowId(item) === rowId);
      if (quote) {
        gridApiRef.current?.applyTransactionAsync({
          update: [mapQuoteToMarketBoardRow(quote)],
        });
      }
    }, 900);
  }, []);
  const handleRealtimeQuoteUpdate = useCallback((update: MarketQuoteUpdate) => {
    const result = applyQuoteUpdate(quotesRef.current, update);

    if (result.updatedQuote == null) {
      return;
    }

    quotesRef.current = result.quotes;
    const rowId = getRowId(result.updatedQuote);
    const nextFlashClassesByRow = {
      ...flashClassesByRowRef.current,
      [rowId]: result.flashClasses,
    };
    flashClassesByRowRef.current = nextFlashClassesByRow;
    setFlashClassesByRow(nextFlashClassesByRow);
    setQuotes(result.quotes);
    gridApiRef.current?.applyTransactionAsync({
      update: [mapQuoteToMarketBoardRow(result.updatedQuote, result.flashClasses)],
    });
    scheduleFlashClear(rowId);
  }, [scheduleFlashClear]);
  const realtimeConnection = useQuoteHubConnection({
    marketBoardSubscription,
    onMarketIndexUpdate: setLatestMarketIndexUpdate,
    onQuoteUpdate: handleRealtimeQuoteUpdate,
    onTradeUpdate: setLatestTradeUpdate,
    onStreamStatus: setStreamStatus,
  });
  const handleGridReady = useCallback((event: GridReadyEvent<MarketBoardRow>) => {
    gridApiRef.current = event.api;
  }, []);
  const handleRowClicked = useCallback((event: RowClickedEvent<MarketBoardRow>) => {
    if (event.data == null) {
      return;
    }

    setSelectedSymbol({
      boardId: event.data.boardId,
      symbol: event.data.symbol,
    });
  }, []);

  useEffect(() => {
    const nextQuotes = quotesQuery.data ?? [];
    Object.values(flashClearTimersRef.current).forEach((timerId) => window.clearTimeout(timerId));
    flashClearTimersRef.current = {};
    flashClassesByRowRef.current = {};
    setFlashClassesByRow({});
    quotesRef.current = nextQuotes;
    setQuotes(nextQuotes);
  }, [quotesQuery.data]);

  useEffect(() => {
    if (selectedSymbol == null || quotes.some((quote) => getRowId(quote) === `${selectedSymbol.boardId}:${selectedSymbol.symbol}`)) {
      return;
    }

    setSelectedSymbol(null);
  }, [quotes, selectedSymbol]);

  useEffect(() => {
    return () => {
      Object.values(flashClearTimersRef.current).forEach((timerId) => window.clearTimeout(timerId));
      flashClearTimersRef.current = {};
    };
  }, []);

  const rows = useMemo(() => {
    const normalizedSearch = deferredSymbolSearch.trim().toUpperCase();
    const mappedRows = quotes.map((quote) => mapQuoteToMarketBoardRow(quote, flashClassesByRow[getRowId(quote)]));

    if (!normalizedSearch) {
      return mappedRows;
    }

    return mappedRows.filter(
      (row) => row.symbol.includes(normalizedSearch) || row.displayName.toUpperCase().includes(normalizedSearch),
    );
  }, [deferredSymbolSearch, flashClassesByRow, quotes]);
  const selectedLiveQuote = useMemo(() => {
    if (selectedSymbol == null) {
      return null;
    }

    return quotes.find((quote) => getRowId(quote) === `${selectedSymbol.boardId}:${selectedSymbol.symbol}`) ?? null;
  }, [quotes, selectedSymbol]);
  const realtimeLabel = getRealtimeLabel(realtimeConnection.status);
  const realtimeToneClass = getRealtimeToneClass(realtimeConnection.status);

  return (
    <section className="flex min-h-[620px] min-w-0 flex-col border border-market-border bg-market-bg">
      <MarketIndexOverview latestUpdate={latestMarketIndexUpdate} />

      <div className="flex min-h-10 flex-wrap items-center gap-2 border-b border-market-border bg-market-surface px-2 py-1">
        <label className="sr-only" htmlFor="symbol-search">
          Search symbol
        </label>
        <div className="relative">
          <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-market-text-subtle">
              <circle cx="11" cy="11" r="8"></circle>
              <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
            </svg>
          </div>
          <input
            className="h-8 w-56 rounded border border-market-border bg-market-surface-2 pl-9 pr-3 text-[12px] font-medium text-market-text outline-none placeholder:text-market-text-subtle focus:border-focus-ring"
            id="symbol-search"
            onChange={(event) => setSymbolSearch(event.target.value)}
            placeholder="Tìm kiếm CK"
            type="search"
            value={symbolSearch}
          />
        </div>

        <WatchlistPanel />

        <div className="group relative z-[100]">
          <button
            className={`flex h-8 items-center justify-between gap-1.5 border px-3 text-[12px] font-medium ${activeFilter.kind === 'index'
              ? 'border-state-online border-x-0 border-b-0 border-t-2 text-market-text'
              : 'border-transparent text-[#c8c6d4] hover:bg-market-surface-2'
              }`}
            type="button"
          >
            <span className="truncate">
              {systemIndexLists.find((list) => list.code === selectedIndexCode)?.label || 'VN30'}
            </span>
            <svg className="shrink-0 text-[#c8c6d4]" width="9" height="6" viewBox="0 0 10 6" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M1 1L5 5L9 1" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
          </button>

          <div className="absolute left-0 top-full hidden min-w-[280px] bg-[#312b40] shadow-xl group-hover:block">
            <div className="columns-2 gap-0 py-2">
              {systemIndexLists.map((marketList) => (
                <button
                  key={marketList.id}
                  type="button"
                  className={`block w-full text-left px-4 py-2.5 text-[12px] font-medium hover:bg-[#555162] ${selectedIndexCode === marketList.code ? 'bg-[#555162] text-white' : 'text-[#c8c6d4]'
                    }`}
                  onClick={() => {
                    setSelectedIndexCode(marketList.code);
                    setActiveFilter({ kind: 'index', list: marketList });
                  }}
                >
                  {marketList.label}
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-1" aria-label="Exchange filters">
          {systemExchangeLists.map((marketList) => (
            <button
              className={`flex h-8 items-center justify-between gap-1.5 border px-3 text-[12px] font-medium ${activeFilter.kind === 'exchange' && marketList.code === activeFilter.list.code
                ? 'border-state-online border-x-0 border-b-0 border-t-2 text-market-text'
                : 'border-transparent text-[#c8c6d4] hover:bg-market-surface-2 hover:text-market-text'
                }`}
              key={marketList.id}
              type="button"
              onClick={() => setActiveFilter({ kind: 'exchange', list: marketList })}
            >
              <span className="truncate">{marketList.label}</span>
              {marketList.code !== 'UPCOM' && (
                <svg className="shrink-0 text-[#c8c6d4]" width="9" height="6" viewBox="0 0 10 6" fill="none" xmlns="http://www.w3.org/2000/svg">
                  <path d="M1 1L5 5L9 1" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              )}
            </button>
          ))}
        </div>
        <div className="ml-auto flex items-center gap-4 text-[11px]">
          <span className="font-medium text-market-text-muted">
            {rows.length > 0 ? `${rows.length} mã | Cập nhật ${rows[0].updatedTime}` : 'Chưa có dữ liệu'}
          </span>
          <div className="flex items-center gap-2 font-semibold">
            <span className="rounded-sm border border-market-border-strong bg-market-surface-2 px-2 py-1 text-state-warning">
              REST snapshot
            </span>
            <span className="inline-flex items-center gap-1 rounded-sm border border-market-border px-2 py-1 text-market-text-muted">
              <span className={`size-2 rounded-full ${realtimeToneClass}`} aria-hidden="true" />
              {realtimeLabel}
            </span>
          </div>
        </div>
      </div>

      {quotesQuery.isPending ? (
        <BoardState label="Loading market board" />
      ) : null}

      {quotesQuery.isError ? (
        <BoardState label={quotesQuery.error.message} tone="error" />
      ) : null}

      {quotesQuery.isSuccess && rows.length === 0 ? <BoardState label="Không có mã cho bảng này" /> : null}

      {quotesQuery.isSuccess && rows.length > 0 ? (
        <div className="min-h-0 flex-1" data-testid="market-board-grid">
          <AgGridReact
            autoSizeStrategy={{
              type: 'fitGridWidth',
              defaultMinWidth: 40,
              columnLimits: [
                { colId: 'pinSymbol', minWidth: 28, maxWidth: 28 },
                { colId: 'symbol', minWidth: 52, maxWidth: 64 },
                { colId: 'totalVolume', minWidth: 72 },
                { colId: 'foreignBuyVolume', minWidth: 72 },
                { colId: 'foreignSellVolume', minWidth: 72 },
                { colId: 'foreignRoom', minWidth: 90 },
              ],
            }}
            columnDefs={marketBoardColumnDefs}
            defaultColDef={defaultMarketBoardColumnDef}
            defaultColGroupDef={defaultMarketBoardColumnGroupDef}
            getRowId={(params) => params.data.id}
            headerHeight={30}
            onGridReady={handleGridReady}
            onRowClicked={handleRowClicked}
            rowData={rows}
            suppressCellFocus
            suppressHorizontalScroll
            theme={marketBoardTheme}
            tooltipShowDelay={300}
          />
        </div>
      ) : null}

      <SymbolDetailPanel
        liveQuote={selectedLiveQuote}
        liveTrade={latestTradeUpdate}
        selection={selectedSymbol}
        onClose={() => setSelectedSymbol(null)}
      />

      <OrderTicketPanel
        liveQuote={selectedLiveQuote}
        selection={selectedSymbol}
      />

      <div className="border-t border-market-border bg-market-surface px-3 py-2 text-[11px] text-market-text-muted">
        {streamStatus?.message ?? 'REST snapshot loaded; SignalR applies realtime quote updates.'}
      </div>
    </section>
  );
}

function getRowId(quote: Pick<MarketQuote, 'boardId' | 'symbol'>) {
  return `${quote.boardId}:${quote.symbol}`;
}

function BoardState({ label, tone = 'muted' }: { label: string; tone?: 'muted' | 'error' }) {
  return (
    <div
      aria-busy={label === 'Loading market board'}
      className={`grid flex-1 place-items-center px-4 py-10 text-sm font-semibold ${tone === 'error' ? 'text-state-error' : 'text-market-text-muted'
        }`}
    >
      {label}
    </div>
  );
}

function getRealtimeLabel(status: ReturnType<typeof useQuoteHubConnection>['status']) {
  switch (status) {
    case 'connected':
      return 'Realtime on';
    case 'connecting':
      return 'Realtime connecting';
    case 'reconnecting':
      return 'Realtime reconnecting';
    case 'error':
      return 'Realtime offline';
    case 'disconnected':
      return 'Realtime disconnected';
    case 'idle':
      return 'Realtime idle';
  }
}

function getRealtimeToneClass(status: ReturnType<typeof useQuoteHubConnection>['status']) {
  switch (status) {
    case 'connected':
      return 'bg-state-online';
    case 'connecting':
    case 'reconnecting':
      return 'bg-state-warning';
    case 'disconnected':
    case 'error':
    case 'idle':
      return 'bg-state-error';
  }
}
