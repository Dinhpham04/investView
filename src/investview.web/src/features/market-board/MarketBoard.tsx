import { useDeferredValue, useMemo, useState } from 'react';
import { AgGridReact } from 'ag-grid-react';
import { useMarketQuotesQuery } from './useMarketQuotesQuery';
import { mapQuoteToMarketBoardRow } from './marketBoardFormatters';
import { defaultMarketBoardColumnDef, defaultMarketBoardColumnGroupDef, marketBoardColumnDefs } from './marketBoardColumns';
import { marketBoardTheme } from './marketBoardTheme';
import { systemExchangeLists, systemIndexLists, type SystemMarketList } from './marketLists';

type ActiveMarketFilter =
  | { kind: 'exchange'; list: SystemMarketList }
  | { kind: 'index'; list: SystemMarketList };

export function MarketBoard() {
  const [selectedIndexCode, setSelectedIndexCode] = useState('VN30');
  const [activeFilter, setActiveFilter] = useState<ActiveMarketFilter>({
    kind: 'index',
    list: systemIndexLists.find((marketList) => marketList.code === 'VN30') ?? systemIndexLists[0],
  });
  const [symbolSearch, setSymbolSearch] = useState('');
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
  const rows = useMemo(() => {
    const normalizedSearch = deferredSymbolSearch.trim().toUpperCase();
    const mappedRows = quotesQuery.data?.map(mapQuoteToMarketBoardRow) ?? [];

    if (!normalizedSearch) {
      return mappedRows;
    }

    return mappedRows.filter(
      (row) => row.symbol.includes(normalizedSearch) || row.displayName.toUpperCase().includes(normalizedSearch),
    );
  }, [deferredSymbolSearch, quotesQuery.data]);

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
          <span className="rounded-sm border border-market-border px-2 py-1 text-market-text-muted">Mock data</span>
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
            rowData={rows}
            suppressCellFocus
            suppressHorizontalScroll
            theme={marketBoardTheme}
            tooltipShowDelay={300}
          />
        </div>
      ) : null}

      <div className="border-t border-market-border bg-market-surface px-3 py-2 text-[11px] text-market-text-muted">
        Dữ liệu hiện tại là snapshot từ REST API. Realtime SignalR sẽ được triển khai ở Task 7.
      </div>
    </section>
  );
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
