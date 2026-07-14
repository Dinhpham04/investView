import type { OrderSide, PortfolioSnapshot, SimulatedOrder } from '../../shared/types/trading';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { formatPrice, formatQuantity } from '../market-board/marketBoardFormatters';
import { formatMoney } from '../trading/tradingFormatters';
import { useDemoSession } from '../auth/useDemoSession';

export type AccountTab = 'orders' | 'conditional' | 'watchlist' | 'assets';

type OrderAccountAreaProps = {
  activeTab: AccountTab;
  cancellingOrderId?: string | null;
  onCancelOrder?: (order: SimulatedOrder) => void;
  onEditOrder?: (order: SimulatedOrder) => void;
  onTabChange: (tab: AccountTab) => void;
  orders: SimulatedOrder[];
  ordersLoading: boolean;
  portfolio: PortfolioSnapshot | null;
  portfolioError: unknown;
  portfolioLoading: boolean;
};

const accountTabs: Array<{ id: AccountTab; label: string }> = [
  { id: 'orders', label: 'Sổ lệnh' },
  { id: 'conditional', label: 'Sổ lệnh điều kiện' },
  { id: 'watchlist', label: 'Danh mục' },
  { id: 'assets', label: 'Tài sản' },
];

const assetNumberFormatter = new Intl.NumberFormat('vi-VN', {
  maximumFractionDigits: 0,
});

export function OrderAccountArea({
  activeTab,
  cancellingOrderId = null,
  onCancelOrder,
  onEditOrder,
  onTabChange,
  orders,
  ordersLoading,
  portfolio,
  portfolioError,
  portfolioLoading,
}: OrderAccountAreaProps) {
  return (
    <Tabs
      className="min-h-0 flex-1 gap-0"
      value={activeTab}
      onValueChange={(value) => onTabChange(value as AccountTab)}
    >
      <div className="flex h-[36px] shrink-0 items-stretch border-b border-[#393548]">
        <TabsList
          aria-label="Thông tin tài khoản"
          className="h-full min-w-0 flex-1 gap-0 rounded-none bg-transparent p-0"
          variant="line"
        >
          {accountTabs.map((tab) => {
            return (
              <TabsTrigger
                className="h-full min-w-0 rounded-none px-1 py-0 text-[12px] font-semibold text-[#b7b3bf] after:bottom-0 after:inset-x-2 after:bg-[#ef3340] data-active:text-white"
                key={tab.id}
                value={tab.id}
              >
                {tab.label}
              </TabsTrigger>
            );
          })}
        </TabsList>
      </div>

      <TabsContent className="flex min-h-0 flex-col" value="orders">
        <OrdersLedger
          cancellingOrderId={cancellingOrderId}
          isLoading={ordersLoading}
          orders={orders}
          onCancelOrder={onCancelOrder}
          onEditOrder={onEditOrder}
        />
      </TabsContent>
      <TabsContent className="flex min-h-0 flex-col" value="conditional">
        <EmptyAccountTab message="Chưa có lệnh điều kiện" />
      </TabsContent>
      <TabsContent className="flex min-h-0 flex-col" value="watchlist">
        <HoldingsTab portfolio={portfolio} />
      </TabsContent>
      <TabsContent className="flex min-h-0 flex-col" value="assets">
        <AssetsTab error={portfolioError} isLoading={portfolioLoading} portfolio={portfolio} />
      </TabsContent>
    </Tabs>
  );
}

function OrdersLedger({
  cancellingOrderId,
  isLoading,
  onCancelOrder,
  onEditOrder,
  orders,
}: {
  cancellingOrderId: string | null;
  isLoading: boolean;
  onCancelOrder?: (order: SimulatedOrder) => void;
  onEditOrder?: (order: SimulatedOrder) => void;
  orders: SimulatedOrder[];
}) {
  const totalOrdered = orders.reduce((total, order) => total + order.quantity, 0);
  const totalFilled = orders.reduce((total, order) => total + order.filledQuantity, 0);

  return (
    <div className="flex min-h-0 flex-1 flex-col bg-[#1b1828]" role="table" aria-label="Sổ lệnh mô phỏng">
      <div className="flex h-[36px] shrink-0 items-center border-b border-[#343143] bg-[#1d1a2a] px-2 text-[12px] font-semibold text-[#aaa6b4]">
        <span>
          Tổng KL đặt: <strong className="text-[#ddd9e5]">{formatQuantity(totalOrdered)}</strong>
        </span>
        <span className="mx-1.5 text-[#696374]">·</span>
        <span>
          Tổng KL khớp: <strong className="text-[#ddd9e5]">{formatQuantity(totalFilled)}</strong>
        </span>
      </div>

      <div className="grid min-h-[38px] shrink-0 grid-cols-[1fr_1fr_0.8fr_1fr_1fr_1fr_1.1fr] items-center bg-[#393647] px-2 text-center text-[11px] font-bold leading-[14px] text-[#c7c3ce]" role="row">
        <span className="text-left" role="columnheader">Mã CK</span>
        <span role="columnheader">Mua/Bán</span>
        <span role="columnheader">Loại</span>
        <span role="columnheader">KL Khớp<br />KL đặt</span>
        <span role="columnheader">Giá khớp TB<br />Giá đặt</span>
        <span role="columnheader">Trạng thái</span>
        <span role="columnheader">Sửa/Hủy</span>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto">
        {isLoading ? (
          <EmptyAccountTab message="Đang tải sổ lệnh..." />
        ) : orders.length === 0 ? (
          <EmptyAccountTab message="Không tìm thấy lệnh nào" />
        ) : (
          orders.map((order) => (
            <OrderLedgerRow
              cancellingOrderId={cancellingOrderId}
              key={order.id}
              order={order}
              onCancelOrder={onCancelOrder}
              onEditOrder={onEditOrder}
            />
          ))
        )}
      </div>
    </div>
  );
}

function OrderLedgerRow({
  cancellingOrderId,
  onCancelOrder,
  onEditOrder,
  order,
}: {
  cancellingOrderId: string | null;
  onCancelOrder?: (order: SimulatedOrder) => void;
  onEditOrder?: (order: SimulatedOrder) => void;
  order: SimulatedOrder;
}) {
  const status = formatStatus(order.status);
  const canAmend = order.status === 'New';
  const isCancelling = cancellingOrderId === order.id;

  return (
    <div
      aria-label={`${order.symbol} ${formatSide(order.side)} ${order.orderType} ${status}`}
      className="grid min-h-[48px] grid-cols-[1fr_1fr_0.8fr_1fr_1fr_1fr_1.1fr] items-center border-b border-[#343143] px-2 text-center text-[11px] text-[#d7d2df] transition-colors hover:bg-[#242033]"
      role="row"
    >
      <div className="min-w-0 text-left" role="cell">
        <strong className="block truncate text-[12px] font-extrabold text-[#ffe000]">{order.symbol}</strong>
        <span className="mt-0.5 block text-[9px] font-semibold text-[#8f8a9a]">{order.boardId}</span>
      </div>
      <span className={`font-bold ${order.side === 'Buy' ? 'text-[#20d18b]' : 'text-[#ff4255]'}`} role="cell">
        {formatSide(order.side)}
      </span>
      <span className="font-bold text-[#dcd8e5]" role="cell">{order.orderType}</span>
      <div className="tabular-nums" role="cell">
        <p className="font-semibold text-white">{formatQuantity(order.filledQuantity)}</p>
        <p className="mt-0.5 text-[#c8c3d0]">{formatQuantity(order.quantity)}</p>
      </div>
      <div className="tabular-nums" role="cell">
        <p className="font-semibold text-white">{formatPrice(order.averageFillPrice)}</p>
        <p className="mt-0.5 text-[#c8c3d0]">{formatPrice(order.limitPrice)}</p>
      </div>
      <span className={getStatusTextClass(order.status)} role="cell">{status}</span>
      <div className="flex items-center justify-center gap-1" role="cell">
        {canAmend ? (
          <>
            <button
              className="rounded px-1.5 py-0.5 font-semibold text-[#c8c3d0] hover:bg-[#343143] hover:text-white disabled:cursor-not-allowed disabled:opacity-45"
              disabled={isCancelling}
              type="button"
              onClick={() => onEditOrder?.(order)}
            >
              Sửa
            </button>
            <button
              className="rounded px-1.5 py-0.5 font-semibold text-[#ff6577] hover:bg-[#3b1521] disabled:cursor-not-allowed disabled:opacity-45"
              disabled={isCancelling}
              type="button"
              onClick={() => onCancelOrder?.(order)}
            >
              {isCancelling ? '...' : 'Hủy'}
            </button>
          </>
        ) : (
          <span className="text-[#8d8998]">-</span>
        )}
      </div>
    </div>
  );
}

function HoldingsTab({ portfolio }: { portfolio: PortfolioSnapshot | null }) {
  if (portfolio == null || portfolio.holdings.length === 0) {
    return <EmptyAccountTab message="Danh mục chưa có chứng khoán" />;
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col bg-[#1b1828]" role="table" aria-label="Danh mục chứng khoán nắm giữ">
      <div className="grid min-h-[38px] shrink-0 grid-cols-[1.05fr_0.95fr_1fr_1.15fr_1.05fr_0.75fr] items-center bg-[#393647] px-2 text-center text-[11px] font-bold leading-[14px] text-[#c7c3ce]" role="row">
        <span className="text-left" role="columnheader">Mã CK</span>
        <span role="columnheader">KLGD<br />Tổng KL</span>
        <span role="columnheader">Giá TT<br />Giá vốn</span>
        <span role="columnheader">Giá trị TT</span>
        <span role="columnheader">Lãi/Lỗ<br />Lãi/Lỗ (%)</span>
        <span role="columnheader">Bán</span>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto">
        {portfolio.holdings.map((holding) => (
          <HoldingRow holding={holding} key={`${holding.boardId}:${holding.symbol}`} />
        ))}
      </div>
    </div>
  );
}

function HoldingRow({ holding }: { holding: PortfolioSnapshot['holdings'][number] }) {
  const pnlPercent = calculatePercentChange(holding.unrealizedPnL, holding.costValue);
  const pnlClass = holding.unrealizedPnL > 0
    ? 'text-[#20d18b]'
    : holding.unrealizedPnL < 0
      ? 'text-[#ff4255]'
      : 'text-[#dcd8e5]';

  return (
    <div
      aria-label={`${holding.symbol} tổng khối lượng ${formatQuantity(holding.quantity)} giá trị thị trường ${formatMoney(holding.marketValue)}`}
      className="grid min-h-[52px] grid-cols-[1.05fr_0.95fr_1fr_1.15fr_1.05fr_0.75fr] items-center border-b border-[#343143] px-2 text-center text-[11px] text-[#d7d2df] transition-colors hover:bg-[#242033]"
      role="row"
    >
      <div className="min-w-0 text-left" role="cell">
        <strong className="block truncate text-[13px] font-extrabold text-[#ffe000]">{holding.symbol}</strong>
        <p className="mt-0.5 truncate text-[10px] font-semibold text-[#9f9aaa]">
          Có thể bán {formatQuantity(holding.availableQuantity)}
          {holding.pendingReceiveQuantity > 0 ? ` · Chờ về ${formatQuantity(holding.pendingReceiveQuantity)}` : ''}
        </p>
      </div>
      <div className="tabular-nums" role="cell">
        <p className="font-bold text-white">{formatQuantity(holding.availableQuantity)}</p>
        <p className="mt-0.5 text-[#aaa6b4]">{formatQuantity(holding.quantity)}</p>
      </div>
      <div className="tabular-nums" role="cell">
        <p className="font-bold text-white">{formatPrice(holding.lastPrice)}</p>
        <p className="mt-0.5 text-[#aaa6b4]">{formatPrice(holding.averageCost)}</p>
      </div>
      <span className="truncate text-right font-bold tabular-nums text-white" role="cell">
        {formatMoney(holding.marketValue)}
      </span>
      <div className={`tabular-nums ${pnlClass}`} role="cell">
        <p className="font-bold">{formatMoney(holding.unrealizedPnL)}</p>
        <p className="mt-0.5 font-semibold">{formatPercentValue(pnlPercent)}</p>
      </div>
      <div className="flex justify-center" role="cell">
        <button
          className="h-6 rounded bg-[#d81024] px-2 text-[11px] font-bold text-white hover:bg-[#e51a2e] disabled:cursor-not-allowed disabled:bg-[#3a2330] disabled:text-[#8f8a9a]"
          disabled
          title="Chức năng bán nhanh từ danh mục sẽ được bổ sung sau"
          type="button"
        >
          Bán
        </button>
      </div>
    </div>
  );
}

function AssetsTab({
  error,
  isLoading,
  portfolio,
}: {
  error: unknown;
  isLoading: boolean;
  portfolio: PortfolioSnapshot | null;
}) {
  const { session, status } = useDemoSession();

  if (status === 'checking') {
    return <EmptyAccountTab message="Đang xác minh phiên đăng nhập..." />;
  }

  if (session == null) {
    return <EmptyAccountTab message="Đăng nhập ở góc trên bên phải để xem tài sản mô phỏng." />;
  }

  if (error instanceof Error) {
    return (
      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-3">
        <p className="rounded border border-[#5a2735] bg-[#2a1624] px-3 py-2 text-[12px] font-semibold text-[#ff6577]" role="alert">
          {error.message}
        </p>
      </div>
    );
  }

  if (isLoading && portfolio == null) {
    return (
      <div
        className="min-h-0 flex-1 overflow-y-auto bg-[#1b1828]"
        role="status"
        aria-label="Đang tải tài sản"
      >
        <div className="border-b border-[#302d3d]">
          {Array.from({ length: 6 }).map((_, index) => (
            <div
              className={`grid min-h-[36px] grid-cols-[minmax(0,1fr)_96px] items-center gap-3 border-b border-[#302d3d] px-3 ${
                index % 2 === 0 ? 'bg-[#1b1828]' : 'bg-[#2a2738]'
              }`}
              key={index}
            >
              <div className="h-3 w-32 animate-pulse rounded bg-[#403b50]" />
              <div className="ml-auto h-3 w-16 animate-pulse rounded bg-[#403b50]" />
            </div>
          ))}
        </div>
      </div>
    );
  }

  if (portfolio == null) {
    return <EmptyAccountTab message="Chưa có dữ liệu tài sản." />;
  }

  const rows = createAssetSummaryRows(portfolio);

  return (
    <section
      aria-label="Tài sản tài khoản mô phỏng"
      className="min-h-0 flex-1 overflow-y-auto bg-[#1b1828]"
    >
      <div className="border-b border-[#302d3d] text-[12px] font-semibold" role="table" aria-label="Tổng hợp tài sản">
        {rows.map((row, index) => (
          <div
            className={`grid min-h-[36px] grid-cols-[minmax(0,1fr)_minmax(92px,auto)] items-center gap-3 border-b border-[#302d3d] px-3 ${
              index % 2 === 0 ? 'bg-[#1b1828]' : 'bg-[#2a2738]'
            }`}
            key={row.label}
            role="row"
          >
            <span className="truncate text-[#aaa6b4]" role="cell">
              {row.label}
            </span>
            <span className="text-right font-bold tabular-nums text-[#dcd8e5]" role="cell">
              {formatAssetAmount(row.value)}
            </span>
          </div>
        ))}
      </div>
    </section>
  );
}

type AssetSummaryRow = {
  label: string;
  value: number | null;
};

function createAssetSummaryRows(portfolio: PortfolioSnapshot): AssetSummaryRow[] {
  return [
    { label: 'Tổng tài sản TKCK', value: portfolio.totalEquity },
    { label: 'Sức mua tối đa', value: portfolio.totalAvailableCash },
    { label: 'Tổng tài sản thực có', value: portfolio.totalEquity },
    { label: 'Số dư tiền', value: portfolio.totalCash },
    { label: 'Giá trị CK niêm yết', value: portfolio.totalMarketValue },
    { label: 'Tiền có thể rút', value: portfolio.totalAvailableCash },
  ];
}

function formatAssetAmount(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) {
    return '-';
  }

  return assetNumberFormatter.format(value);
}

function calculatePercentChange(value: number, baseValue: number) {
  if (baseValue <= 0) {
    return 0;
  }

  return value / baseValue * 100;
}

function formatPercentValue(value: number) {
  return `${value > 0 ? '+' : ''}${value.toFixed(2)}%`;
}

function EmptyAccountTab({ message }: { message: string }) {
  return (
    <div className="grid min-h-[180px] flex-1 place-items-center px-4 text-center text-[12px] text-[#aaa6b4]">
      {message}
    </div>
  );
}

function formatSide(side: OrderSide) {
  return side === 'Buy' ? 'Mua' : 'Bán';
}

function formatStatus(status: SimulatedOrder['status']) {
  const labels: Record<SimulatedOrder['status'], string> = {
    New: 'Chờ khớp',
    Filled: 'Đã khớp',
    Cancelled: 'Đã hủy',
    Rejected: 'Từ chối',
  };

  return labels[status];
}

function getStatusTextClass(status: SimulatedOrder['status']) {
  const classes: Record<SimulatedOrder['status'], string> = {
    New: 'font-semibold text-[#ffd86b]',
    Filled: 'font-semibold text-[#dcd8e5]',
    Cancelled: 'font-semibold text-[#aaa6b4]',
    Rejected: 'font-semibold text-[#ff5b6b]',
  };

  return classes[status];
}
