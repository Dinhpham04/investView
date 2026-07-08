import { useCallback, useDeferredValue, useEffect, useMemo, useRef, useState } from 'react';
import type { GridApi, GridReadyEvent, RowClickedEvent } from 'ag-grid-community';
import { AgGridReact } from 'ag-grid-react';
import { useMarketQuotesQuery } from './useMarketQuotesQuery';
import { mapQuoteToMarketBoardRow, type MarketBoardFlashClasses, type MarketBoardRow } from './marketBoardFormatters';
import { defaultMarketBoardColumnDef, defaultMarketBoardColumnGroupDef, marketBoardColumnDefs } from './marketBoardColumns';
import { marketBoardTheme } from './marketBoardTheme';
import { systemExchangeLists, systemIndexLists, type SystemMarketList } from './marketLists';
import { applyQuoteUpdate } from './marketBoardRealtime';
import { SymbolDetailPanel } from '../symbol-detail/SymbolDetailPanel';
import type { SymbolDetailSelection } from '../symbol-detail/useSymbolDetailQueries';
import { useQuoteHubConnection } from '../../shared/realtime/useQuoteHubConnection';
import type { MarketQuote, MarketQuoteUpdate, QuoteStreamStatus } from '../../shared/types/market';

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
    onQuoteUpdate: handleRealtimeQuoteUpdate,
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
  const realtimeLabel = getRealtimeLabel(realtimeConnection.status);
  const realtimeToneClass = getRealtimeToneClass(realtimeConnection.status);

  return (
    <section className="flex min-h-[620px] min-w-0 flex-col border border-market-border bg-market-bg">
      <div className="flex min-h-11 flex-wrap items-center justify-between gap-3 border-b border-market-border bg-market-surface px-4 py-2">
        <div>
          <p className="text-[11px] font-semibold text-market-text-muted">Market board</p>
          <h2 className="text-base font-bold leading-tight text-market-text">Bảng giá cơ sở</h2>
        </div>
        <div className="flex items-center gap-2 text-[11px] font-semibold">
          <span className="rounded-sm border border-market-border-strong bg-market-surface-2 px-2 py-1 text-state-warning">
            REST snapshot
          </span>
          <span className="inline-flex items-center gap-1 rounded-sm border border-market-border px-2 py-1 text-market-text-muted">
            <span className={`size-2 rounded-full ${realtimeToneClass}`} aria-hidden="true" />
            {realtimeLabel}
          </span>
        </div>
      </div>

      <div className="flex min-h-10 flex-wrap items-center gap-2 border-b border-market-border bg-market-surface px-2 py-1">
        <label className="sr-only" htmlFor="symbol-search">
          Search symbol
        </label>
        <input
          className="h-8 w-56 border border-market-border bg-market-surface-2 px-3 text-xs font-medium text-market-text outline-none placeholder:text-market-text-subtle focus:border-focus-ring"
          id="symbol-search"
          onChange={(event) => setSymbolSearch(event.target.value)}
          placeholder="Tìm mã CK"
          type="search"
          value={symbolSearch}
        />
        <label className="sr-only" htmlFor="user-market-list">
          User market list
        </label>
        <select
          className="h-8 border border-transparent bg-market-surface px-3 text-xs font-semibold text-market-text-muted outline-none hover:bg-market-surface-2 focus:border-focus-ring"
          id="user-market-list"
          value="watchlist"
          onChange={() => undefined}
        >
          <option value="watchlist">Danh m&#7909;c c&#7911;a t&#244;i</option>
        </select>

        <label className="sr-only" htmlFor="index-market-list">
          Index market list
        </label>
        <select
          className="h-8 border border-transparent bg-market-surface px-3 text-xs font-semibold text-market-text outline-none hover:bg-market-surface-2 focus:border-focus-ring"
          id="index-market-list"
          value={selectedIndexCode}
          onChange={(event) => {
            const selectedMarketList = systemIndexLists.find((marketList) => marketList.code === event.target.value);
            if (selectedMarketList) {
              setSelectedIndexCode(selectedMarketList.code);
              setActiveFilter({ kind: 'index', list: selectedMarketList });
            }
          }}
        >
          {systemIndexLists.map((marketList) => (
            <option key={marketList.id} value={marketList.code}>
              {marketList.label}
            </option>
          ))}
        </select>

        <div className="flex flex-wrap items-center gap-1" aria-label="Exchange filters">
          {systemExchangeLists.map((marketList) => (
            <button
              className={`h-8 border px-3 text-xs font-semibold ${
                activeFilter.kind === 'exchange' && marketList.code === activeFilter.list.code
                  ? 'border-state-online bg-market-surface-2 text-market-text'
                  : 'border-transparent text-market-text-muted hover:bg-market-surface-2 hover:text-market-text'
              }`}
              key={marketList.id}
              type="button"
              onClick={() => setActiveFilter({ kind: 'exchange', list: marketList })}
            >
              {marketList.label}
            </button>
          ))}
        </div>
        <span className="ml-auto text-[11px] font-medium text-market-text-muted">
          {rows.length > 0 ? `${rows.length} mã | Cập nhật ${rows[0].updatedTime}` : 'Chưa có dữ liệu'}
        </span>
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

      <SymbolDetailPanel selection={selectedSymbol} onClose={() => setSelectedSymbol(null)} />

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
      className={`grid flex-1 place-items-center px-4 py-10 text-sm font-semibold ${
        tone === 'error' ? 'text-state-error' : 'text-market-text-muted'
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
