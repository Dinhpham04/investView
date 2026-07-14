import { useMemo } from 'react';
import type { ColDef, ValueFormatterParams } from 'ag-grid-community';
import { AgGridReact } from 'ag-grid-react';
import type { OrderSide, OrderStatus, OrderType, SimulatedOrder } from '../../shared/types/trading';
import { formatPrice, formatQuantity } from '../market-board/marketBoardFormatters';
import { marketBoardTheme } from '../market-board/marketBoardTheme';
import { formatMoney } from '../trading/tradingFormatters';
import { useOrderBook } from './useOrderBook';

type OrderBookViewProps = {
  onEditOrder?: (order: SimulatedOrder) => void;
};

type OrderBookRow = SimulatedOrder & {
  channel: string;
  matchedValue: number;
  openQuantity: number;
};

type OrderBookSummary = {
  matchedBuyValue: number;
  matchedSellValue: number;
  totalFilledQuantity: number;
  totalOpenQuantity: number;
  totalOrderedQuantity: number;
};

export function OrderBookView({ onEditOrder }: OrderBookViewProps) {
  const { cancelOrderMutation, orders, ordersQuery, sessionStatus } = useOrderBook();
  const rows = useMemo<OrderBookRow[]>(
    () => orders.map((order) => ({
      ...order,
      channel: 'Demo',
      matchedValue: getMatchedValue(order),
      openQuantity: getOpenQuantity(order),
    })),
    [orders],
  );
  const summary = useMemo(() => getSummary(rows), [rows]);
  const columnDefs = useMemo<ColDef<OrderBookRow>[]>(
    () => createOrderBookColumns({
      cancellingOrderId: cancelOrderMutation.variables?.id ?? null,
      onCancelOrder: (order) => {
        void cancelOrderMutation.mutateAsync(order).catch(() => undefined);
      },
      onEditOrder,
    }),
    [cancelOrderMutation, onEditOrder],
  );

  if (sessionStatus === 'checking') {
    return <OrderBookState message="Đang xác minh phiên đăng nhập..." />;
  }

  if (sessionStatus !== 'authenticated') {
    return <OrderBookState message="Đăng nhập demo để xem sổ lệnh cơ sở." />;
  }

  if (ordersQuery.isPending) {
    return <OrderBookState message="Đang tải sổ lệnh cơ sở..." />;
  }

  if (ordersQuery.isError) {
    return <OrderBookState message={ordersQuery.error.message} tone="error" />;
  }

  return (
    <section className="flex min-h-0 flex-1 flex-col bg-[#1b1828]" aria-label="Sổ lệnh cơ sở">
      <div className="flex min-h-10 shrink-0 items-center justify-between border-b border-[#343143] bg-[#1d1a2a] px-3 text-[12px] font-semibold text-[#c8c3d0]">
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
          <span>
            Giá trị khớp lệnh Mua:{' '}
            <strong className="text-white">{formatMoney(summary.matchedBuyValue)}</strong>
          </span>
          <span>
            Giá trị khớp lệnh Bán:{' '}
            <strong className="text-white">{formatMoney(summary.matchedSellValue)}</strong>
          </span>
        </div>
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[#9f9aaa]">
          <span>
            KL đặt: <strong className="text-white">{formatQuantity(summary.totalOrderedQuantity)}</strong>
          </span>
          <span>
            KL khớp: <strong className="text-white">{formatQuantity(summary.totalFilledQuantity)}</strong>
          </span>
          <span>
            Chờ khớp: <strong className="text-white">{formatQuantity(summary.totalOpenQuantity)}</strong>
          </span>
        </div>
      </div>

      {cancelOrderMutation.error instanceof Error ? (
        <p className="border-b border-[#343143] bg-[#211e30] px-3 py-1 text-[11px] font-semibold text-[#ff6577]" role="alert">
          {formatOrderErrorMessage(cancelOrderMutation.error)}
        </p>
      ) : null}

      {rows.length === 0 ? (
        <OrderBookState message="Không tìm thấy lệnh nào" />
      ) : (
        <div className="flex min-h-0 flex-1 px-4 py-2">
          <div className="order-book-grid min-h-0 flex-1" data-testid="order-book-grid">
            <AgGridReact
              autoSizeStrategy={{
                type: 'fitGridWidth',
                defaultMinWidth: 82,
                columnLimits: [
                  { colId: 'symbol', minWidth: 96 },
                  { colId: 'actions', minWidth: 94, maxWidth: 110 },
                ],
              }}
              columnDefs={columnDefs}
              defaultColDef={{
                cellClass: 'order-book-cell',
                headerClass: 'header-cell-label-right',
                resizable: false,
                sortable: true,
                suppressMovable: true,
              }}
              getRowId={(params) => params.data.id}
              headerHeight={30}
              rowData={rows}
              rowHeight={44}
              suppressCellFocus
              theme={marketBoardTheme}
            />
          </div>
        </div>
      )}
    </section>
  );
}

function createOrderBookColumns({
  cancellingOrderId,
  onCancelOrder,
  onEditOrder,
}: {
  cancellingOrderId: string | null;
  onCancelOrder: (order: SimulatedOrder) => void;
  onEditOrder?: (order: SimulatedOrder) => void;
}): ColDef<OrderBookRow>[] {
  return [
    {
      colId: 'symbol',
      field: 'symbol',
      headerName: 'Mã CK',
      pinned: 'left',
      cellRenderer: ({ data }: { data?: OrderBookRow }) => data == null ? null : (
        <div className="leading-tight">
          <strong className="block text-[12px] font-extrabold text-[#ffe000]">{data.symbol}</strong>
          <span className="text-[10px] font-semibold text-[#8f8a9a]">{data.boardId}</span>
        </div>
      ),
    },
    {
      field: 'side',
      headerName: 'Mua/Bán',
      cellClass: ({ value }) => `order-book-cell order-book-cell--center ${value === 'Buy' ? 'text-price-up' : 'text-price-down'}`,
      valueFormatter: ({ value }: ValueFormatterParams<OrderBookRow, OrderSide>) => formatSide(value),
    },
    {
      field: 'orderType',
      headerName: 'Loại',
      cellClass: 'order-book-cell order-book-cell--center',
      valueFormatter: ({ value }: ValueFormatterParams<OrderBookRow, OrderType>) => value ?? '-',
    },
    numberColumn('quantity', 'KL đặt'),
    priceColumn('limitPrice', 'Giá đặt'),
    numberColumn('filledQuantity', 'KL khớp'),
    priceColumn('averageFillPrice', 'Giá khớp TB'),
    numberColumn('openQuantity', 'KL chờ khớp'),
    moneyColumn('matchedValue', 'Giá trị khớp'),
    {
      field: 'status',
      headerName: 'Trạng thái',
      cellClass: ({ value }) => `order-book-cell order-book-cell--center ${getStatusTextClass(value)}`,
      valueFormatter: ({ value }: ValueFormatterParams<OrderBookRow, OrderStatus>) => formatStatus(value),
    },
    dateTimeColumn('createdAt', 'Thời gian đặt'),
    dateTimeColumn('updatedAt', 'Thời gian cập nhật'),
    {
      field: 'channel',
      headerName: 'Kênh',
      cellClass: 'order-book-cell order-book-cell--center order-book-cell--muted',
    },
    {
      colId: 'actions',
      headerName: 'Sửa/Hủy',
      sortable: false,
      cellClass: 'order-book-cell order-book-cell--center',
      cellRenderer: ({ data }: { data?: OrderBookRow }) => {
        if (data == null || data.status !== 'New') {
          return <span className="text-[#8f8a9a]">-</span>;
        }

        return (
          <div className="flex items-center justify-center gap-1">
            <button
              className="h-6 rounded bg-[#3a354b] px-2 text-[11px] font-bold text-[#e7e2ee] hover:bg-[#4a455c]"
              type="button"
              onClick={() => onEditOrder?.(data)}
            >
              Sửa
            </button>
            <button
              className="h-6 rounded bg-[#d81024] px-2 text-[11px] font-bold text-white hover:bg-[#e51a2e] disabled:cursor-wait disabled:bg-[#5a2b35]"
              disabled={cancellingOrderId === data.id}
              type="button"
              onClick={() => onCancelOrder(data)}
            >
              {cancellingOrderId === data.id ? 'Đang hủy' : 'Hủy'}
            </button>
          </div>
        );
      },
    },
  ];
}

function numberColumn(field: keyof OrderBookRow, headerName: string): ColDef<OrderBookRow> {
  return {
    field,
    headerClass: 'header-cell-label-right',
    headerName,
    cellClass: 'order-book-cell order-book-cell--number',
    valueFormatter: ({ value }: ValueFormatterParams<OrderBookRow, number>) => formatQuantity(value ?? 0),
  };
}

function priceColumn(field: keyof OrderBookRow, headerName: string): ColDef<OrderBookRow> {
  return {
    field,
    headerClass: 'header-cell-label-right',
    headerName,
    cellClass: 'order-book-cell order-book-cell--number',
    valueFormatter: ({ value }: ValueFormatterParams<OrderBookRow, number | null>) => value == null ? '-' : formatPrice(value),
  };
}

function moneyColumn(field: keyof OrderBookRow, headerName: string): ColDef<OrderBookRow> {
  return {
    field,
    headerClass: 'header-cell-label-right',
    headerName,
    cellClass: 'order-book-cell order-book-cell--number',
    valueFormatter: ({ value }: ValueFormatterParams<OrderBookRow, number>) => formatMoney(value ?? 0),
  };
}

function dateTimeColumn(field: keyof OrderBookRow, headerName: string): ColDef<OrderBookRow> {
  return {
    field,
    headerName,
    cellClass: 'order-book-cell order-book-cell--muted',
    valueFormatter: ({ value }: ValueFormatterParams<OrderBookRow, string>) => formatDateTime(value),
  };
}

function getMatchedValue(order: SimulatedOrder) {
  const executionValue = order.executions.reduce((total, execution) => total + execution.grossAmount, 0);
  if (executionValue > 0) {
    return executionValue;
  }

  return order.averageFillPrice == null ? 0 : order.filledQuantity * order.averageFillPrice;
}

function getOpenQuantity(order: SimulatedOrder) {
  return order.status === 'New' ? Math.max(order.quantity - order.filledQuantity, 0) : 0;
}

function getSummary(rows: OrderBookRow[]): OrderBookSummary {
  return rows.reduce<OrderBookSummary>(
    (summary, row) => ({
      matchedBuyValue: summary.matchedBuyValue + (row.side === 'Buy' ? row.matchedValue : 0),
      matchedSellValue: summary.matchedSellValue + (row.side === 'Sell' ? row.matchedValue : 0),
      totalFilledQuantity: summary.totalFilledQuantity + row.filledQuantity,
      totalOpenQuantity: summary.totalOpenQuantity + row.openQuantity,
      totalOrderedQuantity: summary.totalOrderedQuantity + row.quantity,
    }),
    {
      matchedBuyValue: 0,
      matchedSellValue: 0,
      totalFilledQuantity: 0,
      totalOpenQuantity: 0,
      totalOrderedQuantity: 0,
    },
  );
}

function formatSide(side: OrderSide | null | undefined) {
  return side === 'Buy' ? 'Mua' : side === 'Sell' ? 'Bán' : '-';
}

function formatStatus(status: OrderStatus | null | undefined) {
  const labels: Record<OrderStatus, string> = {
    Cancelled: 'Đã hủy',
    Filled: 'Đã khớp',
    New: 'Chờ khớp',
    Rejected: 'Từ chối',
  };

  return status == null ? '-' : labels[status];
}

function getStatusTextClass(status: OrderStatus | null | undefined) {
  const classes: Record<OrderStatus, string> = {
    Cancelled: 'text-[#8f8a9a]',
    Filled: 'text-[#e7e2ee]',
    New: 'text-[#ffdf5a]',
    Rejected: 'text-[#ff6577]',
  };

  return status == null ? '' : classes[status];
}

function formatDateTime(value: string | null | undefined) {
  if (value == null) {
    return '-';
  }

  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    month: '2-digit',
    second: '2-digit',
    year: 'numeric',
  }).format(new Date(value));
}

function formatOrderErrorMessage(error: Error) {
  if (error.message === 'Only pending orders can be cancelled.') {
    return 'Chỉ lệnh chờ khớp mới có thể hủy.';
  }

  return error.message;
}

function OrderBookState({ message, tone = 'muted' }: { message: string; tone?: 'muted' | 'error' }) {
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
