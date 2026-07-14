import { useMemo } from 'react';
import type { ColDef, ColGroupDef, ValueFormatterParams } from 'ag-grid-community';
import { AgGridReact } from 'ag-grid-react';
import type { PortfolioHolding } from '../../shared/types/trading';
import { marketBoardTheme } from '../market-board/marketBoardTheme';
import { formatPrice, formatQuantity } from '../market-board/marketBoardFormatters';
import { formatMoney } from '../trading/tradingFormatters';
import { usePortfolioHoldings } from './usePortfolioHoldings';

type HoldingsViewProps = {
  onSellHolding?: (holding: PortfolioHolding) => void;
};

type HoldingsRow = PortfolioHolding & {
  sellPendingT0Quantity: number;
  sellPendingT1Quantity: number;
  sellPendingT2Quantity: number;
  portfolioWeightPercent: number;
};

export function HoldingsView({ onSellHolding }: HoldingsViewProps) {
  const { holdingsQuery, holdingsSnapshot, sessionStatus } = usePortfolioHoldings();
  const rows = useMemo<HoldingsRow[]>(() => {
    const totalMarketValue = holdingsSnapshot?.totalMarketValue ?? 0;

    return (holdingsSnapshot?.holdings ?? []).map((holding) => ({
      ...holding,
      portfolioWeightPercent: totalMarketValue > 0 ? holding.marketValue / totalMarketValue * 100 : 0,
      sellPendingT0Quantity: 0,
      sellPendingT1Quantity: 0,
      sellPendingT2Quantity: 0,
    }));
  }, [holdingsSnapshot]);
  const columnDefs = useMemo<Array<ColDef<HoldingsRow> | ColGroupDef<HoldingsRow>>>(() => createHoldingColumns(onSellHolding), [onSellHolding]);

  if (sessionStatus === 'checking') {
    return <HoldingsState message="Đang xác minh phiên đăng nhập..." />;
  }

  if (sessionStatus !== 'authenticated') {
    return <HoldingsState message="Đăng nhập demo để xem danh mục nắm giữ." />;
  }

  if (holdingsQuery.isPending) {
    return <HoldingsState message="Đang tải danh mục nắm giữ..." />;
  }

  if (holdingsQuery.isError) {
    return <HoldingsState message={holdingsQuery.error.message} tone="error" />;
  }

  return (
    <section className="flex min-h-0 flex-1 flex-col bg-[#1b1828]" aria-label="Danh mục nắm giữ">
      <div className="flex min-h-10 shrink-0 items-center justify-between border-b border-[#343143] bg-[#1d1a2a] px-3 text-[12px] font-semibold text-[#c8c3d0]">
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
          <span>
            Tổng KL: <strong className="text-white">{formatQuantity(holdingsSnapshot?.totalQuantity ?? 0)}</strong>
          </span>
          <span>
            Có thể bán: <strong className="text-white">{formatQuantity(holdingsSnapshot?.totalAvailableQuantity ?? 0)}</strong>
          </span>
          <span>
            Chờ về: <strong className="text-white">{formatQuantity(holdingsSnapshot?.totalPendingReceiveQuantity ?? 0)}</strong>
          </span>
        </div>
        <span className="tabular-nums text-[#9f9aaa]">
          Giá trị TT: {formatMoney(holdingsSnapshot?.totalMarketValue ?? 0)}
        </span>
      </div>

      {rows.length === 0 ? (
        <HoldingsState message="Danh mục chưa có mã chứng khoán nào" />
      ) : (
        <div className="holdings-grid min-h-0 flex-1" data-testid="holdings-grid">
          <AgGridReact
            autoSizeStrategy={{
              type: 'fitGridWidth',
              defaultMinWidth: 74,
              columnLimits: [
                { colId: 'symbol', minWidth: 96 },
                { colId: 'sellAction', minWidth: 64, maxWidth: 72 },
              ],
            }}
            columnDefs={columnDefs}
            defaultColDef={{
              cellClass: 'holdings-cell',
              resizable: false,
              sortable: true,
              suppressMovable: true,
            }}
            getRowId={(params) => `${params.data.boardId}:${params.data.symbol}`}
            headerHeight={28}
            rowData={rows}
            rowHeight={38}
            suppressCellFocus
            theme={marketBoardTheme}
          />
        </div>
      )}
    </section>
  );
}

function createHoldingColumns(onSellHolding?: (holding: PortfolioHolding) => void): Array<ColDef<HoldingsRow> | ColGroupDef<HoldingsRow>> {
  return [
    {
      colId: 'symbol',
      field: 'symbol',
      headerName: 'Mã CK',
      pinned: 'left',
      cellRenderer: ({ data }: { data?: HoldingsRow }) => data == null ? null : (
        <div className="leading-tight">
          <strong className="block text-[12px] font-extrabold text-[#ffe000]">{data.symbol}</strong>
          <span className="text-[10px] font-semibold text-[#8f8a9a]">{data.boardId}</span>
        </div>
      ),
    },
    numberColumn('quantity', 'Tổng KL'),
    numberColumn('availableQuantity', 'KLGD'),
    {
      children: [
        numberColumn('pendingT0Quantity', 'T0'),
        numberColumn('pendingT1Quantity', 'T1'),
        numberColumn('pendingT2Quantity', 'T2'),
      ],
      headerClass: 'header-group-cell-label-center',
      headerName: 'Mua chờ về',
    },
    {
      children: [
        numberColumn('sellPendingT0Quantity', 'T0'),
        numberColumn('sellPendingT1Quantity', 'T1'),
        numberColumn('sellPendingT2Quantity', 'T2'),
      ],
      headerClass: 'header-group-cell-label-center',
      headerName: 'Bán chờ giao',
    },
    moneyColumn('averageCost', 'Giá vốn', formatPrice),
    moneyColumn('lastPrice', 'Giá TT', formatPrice),
    moneyColumn('costValue', 'Vốn', formatMoney),
    moneyColumn('marketValue', 'Giá trị TT', formatMoney),
    moneyColumn('unrealizedPnL', 'Lãi/Lỗ', formatMoney, pnlCellClass),
    moneyColumn('unrealizedPnLPercent', 'Lãi/Lỗ (%)', formatPercent, pnlCellClass),
    {
      field: 'nextAvailableDate',
      headerName: 'Quyền',
      cellClass: 'holdings-cell holdings-cell--muted',
      valueFormatter: ({ value }: ValueFormatterParams<HoldingsRow, string | null>) => value == null ? '-' : `Về ${formatDate(value)}`,
    },
    moneyColumn('portfolioWeightPercent', '%DM', formatPercent),
    {
      colId: 'sellAction',
      headerName: 'Bán',
      sortable: false,
      cellClass: 'holdings-cell holdings-cell--center',
      cellRenderer: ({ data }: { data?: HoldingsRow }) => (
        <button
          className="h-6 rounded bg-[#d81024] px-2 text-[11px] font-bold text-white hover:bg-[#e51a2e] disabled:cursor-not-allowed disabled:bg-[#3a2330] disabled:text-[#8f8a9a]"
          disabled={data == null || data.availableQuantity <= 0}
          type="button"
          onClick={() => data != null && onSellHolding?.(data)}
        >
          Bán
        </button>
      ),
    },
  ];
}

function numberColumn(field: keyof HoldingsRow, headerName: string): ColDef<HoldingsRow> {
  return {
    field,
    headerClass: 'header-cell-label-right',
    headerName,
    cellClass: 'holdings-cell holdings-cell--number',
    valueFormatter: ({ value }: ValueFormatterParams<HoldingsRow, number>) => formatQuantity(value ?? 0),
  };
}

function moneyColumn(
  field: keyof HoldingsRow,
  headerName: string,
  formatter: (value: number) => string,
  cellClass?: (params: { value: number | null | undefined }) => string,
): ColDef<HoldingsRow> {
  return {
    field,
    headerClass: 'header-cell-label-right',
    headerName,
    cellClass: cellClass ?? 'holdings-cell holdings-cell--number',
    valueFormatter: ({ value }: ValueFormatterParams<HoldingsRow, number>) => formatter(value ?? 0),
  };
}

function pnlCellClass({ value }: { value: number | null | undefined }) {
  if ((value ?? 0) > 0) {
    return 'holdings-cell holdings-cell--number text-price-up';
  }

  if ((value ?? 0) < 0) {
    return 'holdings-cell holdings-cell--number text-price-down';
  }

  return 'holdings-cell holdings-cell--number';
}

function formatPercent(value: number) {
  return `${value > 0 ? '+' : ''}${value.toFixed(2)}%`;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(new Date(`${value}T00:00:00+07:00`));
}

function HoldingsState({ message, tone = 'muted' }: { message: string; tone?: 'muted' | 'error' }) {
  return (
    <div
      className={`grid min-h-0 flex-1 place-items-center bg-[#1b1828] px-4 py-12 text-center text-[12px] font-semibold ${
        tone === 'error' ? 'text-[#ff6577]' : 'text-[#aaa6b4]'
      }`}
      role={tone === 'error' ? 'alert' : 'status'}
    >
      {message}
    </div>
  );
}
