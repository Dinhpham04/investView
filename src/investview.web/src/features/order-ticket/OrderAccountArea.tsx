import type { OrderSide, PortfolioSnapshot, SimulatedOrder } from '../../shared/types/trading';
import {
  BriefcaseBusinessIcon,
  ChevronDownIcon,
  Clock3Icon,
  FilterIcon,
  LandmarkIcon,
  MaximizeIcon,
  PieChartIcon,
  TrendingUpIcon,
  WalletIcon,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { formatPrice, formatQuantity } from '../market-board/marketBoardFormatters';
import { formatMoney } from '../trading/tradingFormatters';
import { useDemoSession } from '../auth/useDemoSession';

export type AccountTab = 'orders' | 'conditional' | 'watchlist' | 'assets';

type OrderAccountAreaProps = {
  activeTab: AccountTab;
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

const assetTimeFormatter = new Intl.DateTimeFormat('vi-VN', {
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  timeZone: 'Asia/Ho_Chi_Minh',
});

export function OrderAccountArea({
  activeTab,
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
      <div className="flex h-[49px] shrink-0 items-stretch border-b border-[#393548]">
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
        <div className="flex shrink-0 items-center gap-2 px-2 text-[#aaa6b4]" aria-hidden="true">
          <MaximizeIcon className="size-3.5" />
          <FilterIcon className="size-3.5" />
          <ChevronDownIcon className="size-3.5" />
        </div>
      </div>

      <TabsContent className="flex min-h-0 flex-col" value="orders">
        <OrdersLedger isLoading={ordersLoading} orders={orders} />
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

function OrdersLedger({ isLoading, orders }: { isLoading: boolean; orders: SimulatedOrder[] }) {
  const totalOrdered = orders.reduce((total, order) => total + order.quantity, 0);
  const totalFilled = orders.reduce((total, order) => total + order.filledQuantity, 0);

  return (
    <div className="flex min-h-0 flex-1 flex-col" role="table" aria-label="Sổ lệnh mô phỏng">
      <div className="h-[36px] shrink-0 px-2 py-2 text-[12px] text-[#aaa6b4]">
        Tổng KL đặt: <strong className="text-[#ddd9e5]">{formatQuantity(totalOrdered)}</strong>
        <span className="mx-1.5">·</span>
        Tổng KL khớp: <strong className="text-[#ddd9e5]">{formatQuantity(totalFilled)}</strong>
      </div>
      <div className="grid min-h-[42px] shrink-0 grid-cols-[52px_57px_82px_85px_65px_55px] items-center rounded-t bg-[#393647] px-1 text-center text-[11px] font-bold leading-[14px] text-[#c7c3ce]" role="row">
        <span role="columnheader">Mã CK</span>
        <span role="columnheader">Mua/Bán</span>
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
            <div
              aria-label={`${order.symbol} ${formatSide(order.side)} ${formatStatus(order.status)}`}
              className="grid min-h-[46px] grid-cols-[52px_57px_82px_85px_65px_55px] items-center border-b border-[#343143] px-1 text-center text-[11px]"
              key={order.id}
              role="row"
            >
              <strong className="text-[#ffe000]" role="cell">{order.symbol}</strong>
              <span className={order.side === 'Buy' ? 'text-[#20d18b]' : 'text-[#ff4255]'} role="cell">{formatSide(order.side)}</span>
              <span role="cell">{formatQuantity(order.filledQuantity)}<br />{formatQuantity(order.quantity)}</span>
              <span role="cell">{formatPrice(order.averageFillPrice)}<br />{formatPrice(order.limitPrice)}</span>
              <span role="cell">{formatStatus(order.status)}</span>
              <span className="text-[#8d8998]" role="cell">-</span>
            </div>
          ))
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
    <div className="min-h-0 flex-1 overflow-y-auto">
      {portfolio.holdings.map((holding) => (
        <div className="grid grid-cols-[1fr_auto_auto] gap-4 border-b border-[#343143] px-3 py-2" key={`${holding.boardId}:${holding.symbol}`}>
          <strong className="text-[#ffe000]">{holding.symbol}</strong>
          <span>{formatQuantity(holding.quantity)}</span>
          <span>{formatMoney(holding.marketValue)}</span>
        </div>
      ))}
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
      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-3" role="status" aria-label="Đang tải tài sản">
        <div className="space-y-3">
          <div className="h-[112px] animate-pulse rounded border border-[#343143] bg-[#211e30]" />
          <div className="grid grid-cols-2 gap-2">
            <div className="h-[72px] animate-pulse rounded border border-[#343143] bg-[#211e30]" />
            <div className="h-[72px] animate-pulse rounded border border-[#343143] bg-[#211e30]" />
          </div>
        </div>
      </div>
    );
  }

  if (portfolio == null) {
    return <EmptyAccountTab message="Chưa có dữ liệu tài sản." />;
  }

  const cashPercent = calculatePercent(portfolio.totalCash, portfolio.totalEquity);
  const stockPercent = calculatePercent(portfolio.totalMarketValue, portfolio.totalEquity);
  const pnlTone = portfolio.totalUnrealizedPnL > 0
    ? 'text-[#20d18b]'
    : portfolio.totalUnrealizedPnL < 0
      ? 'text-[#ff4255]'
      : 'text-[#dcd8e5]';

  return (
    <section
      aria-label="Tài sản tài khoản mô phỏng"
      className="min-h-0 flex-1 overflow-y-auto bg-[#171522]"
    >
      <div className="space-y-3 px-3 py-3">
        <div className="rounded border border-[#393548] bg-[#211e30] p-3 shadow-[inset_0_1px_0_rgba(255,255,255,0.03)]">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="text-[10px] font-extrabold uppercase text-[#ffb800]">Simulated trading</p>
              <h3 className="mt-0.5 truncate text-[13px] font-bold text-white">{session.user.displayName}</h3>
              <p className="mt-1 flex items-center gap-1 text-[11px] text-[#9f9aaa]">
                <Clock3Icon className="size-3" />
                Cập nhật {formatAssetTime(portfolio.updatedAt)}
              </p>
            </div>
            <span className="rounded border border-[#3c6d62] bg-[#142c2a] px-2 py-1 text-[10px] font-bold uppercase text-[#38d3ad]">
              Demo
            </span>
          </div>

          <div className="mt-4">
            <p className="text-[11px] font-semibold text-[#9f9aaa]">Tổng tài sản</p>
            <p className="mt-0.5 text-[22px] font-extrabold leading-none text-white">
              {formatMoney(portfolio.totalEquity)}
            </p>
          </div>

          <div className="mt-3 grid grid-cols-2 gap-2">
            <AssetMetric icon={WalletIcon} label="Tiền mặt" value={formatMoney(portfolio.totalCash)} />
            <AssetMetric icon={LandmarkIcon} label="Sức mua" value={formatMoney(portfolio.totalAvailableCash)} />
            <AssetMetric icon={BriefcaseBusinessIcon} label="Giá trị CK" value={formatMoney(portfolio.totalMarketValue)} />
            <AssetMetric className={pnlTone} icon={TrendingUpIcon} label="Lãi/lỗ tạm tính" value={formatMoney(portfolio.totalUnrealizedPnL)} />
          </div>
        </div>

        <div className="rounded border border-[#343143] bg-[#1d1a2b] p-3">
          <div className="mb-3 flex items-center justify-between">
            <p className="flex items-center gap-2 text-[12px] font-bold text-white">
              <PieChartIcon className="size-4 text-[#38d3ad]" />
              Phân bổ tài sản
            </p>
            <span className="text-[11px] font-semibold text-[#9f9aaa]">{portfolio.holdings.length} mã</span>
          </div>
          <AllocationRow label="Tiền mặt" percent={cashPercent} value={formatMoney(portfolio.totalCash)} variant="cash" />
          <AllocationRow label="Chứng khoán" percent={stockPercent} value={formatMoney(portfolio.totalMarketValue)} variant="stock" />
        </div>

        <div className="rounded border border-[#343143] bg-[#1d1a2b]">
          <div className="flex items-center justify-between border-b border-[#343143] px-3 py-2">
            <p className="text-[12px] font-bold text-white">Danh mục nắm giữ</p>
            <span className="text-[11px] font-semibold text-[#9f9aaa]">{portfolio.holdings.length} mã</span>
          </div>
          {portfolio.holdings.length === 0 ? (
            <div className="px-3 py-8 text-center text-[12px] text-[#9f9aaa]">Danh mục chưa có chứng khoán</div>
          ) : (
            <div className="divide-y divide-[#343143]">
              {portfolio.holdings.map((holding) => (
                <div className="grid grid-cols-[1fr_auto] gap-3 px-3 py-2" key={`${holding.boardId}:${holding.symbol}`}>
                  <div className="min-w-0">
                    <strong className="text-[13px] text-[#ffe000]">{holding.symbol}</strong>
                    <p className="mt-0.5 text-[11px] text-[#9f9aaa]">
                      {formatQuantity(holding.quantity)} cp · Giá vốn {formatPrice(holding.averageCost)}
                    </p>
                  </div>
                  <div className="text-right">
                    <p className="text-[12px] font-bold text-white">{formatMoney(holding.marketValue)}</p>
                    <p className={`mt-0.5 text-[11px] font-semibold ${holding.unrealizedPnL > 0 ? 'text-[#20d18b]' : holding.unrealizedPnL < 0 ? 'text-[#ff4255]' : 'text-[#9f9aaa]'}`}>
                      {formatMoney(holding.unrealizedPnL)}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="rounded border border-[#343143] bg-[#1d1a2b]">
          <div className="border-b border-[#343143] px-3 py-2">
            <p className="text-[12px] font-bold text-white">Tiền theo tài khoản</p>
          </div>
          {portfolio.cashAccounts.length === 0 ? (
            <div className="px-3 py-5 text-[12px] text-[#9f9aaa]">Không có tài khoản tiền chi tiết.</div>
          ) : (
            <div className="divide-y divide-[#343143]">
              {portfolio.cashAccounts.map((account) => (
                <div className="grid grid-cols-[1fr_auto] gap-3 px-3 py-2" key={account.currency}>
                  <span className="text-[12px] font-semibold text-[#dcd8e5]">{account.currency}</span>
                  <span className="text-right text-[12px] font-bold text-white">{formatMoney(account.availableBalance)}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}

function AssetMetric({
  className = 'text-[#dcd8e5]',
  icon: Icon,
  label,
  value,
}: {
  className?: string;
  icon: LucideIcon;
  label: string;
  value: string;
}) {
  return (
    <div className="rounded border border-[#363247] bg-[#181625] px-2.5 py-2">
      <p className="flex items-center gap-1.5 text-[10px] font-bold uppercase text-[#8f8a9a]">
        <Icon className="size-3.5" />
        {label}
      </p>
      <p className={`mt-1 truncate text-[12px] font-extrabold ${className}`}>{value}</p>
    </div>
  );
}

function AllocationRow({
  label,
  percent,
  value,
  variant,
}: {
  label: string;
  percent: number;
  value: string;
  variant: 'cash' | 'stock';
}) {
  const barClass = variant === 'cash' ? 'bg-[#38d3ad]' : 'bg-[#ffd600]';

  return (
    <div className="not-first:mt-3">
      <div className="mb-1.5 flex items-center justify-between gap-3 text-[11px]">
        <span className="font-semibold text-[#c9c4d1]">{label}</span>
        <span className="font-bold text-white">{value}</span>
      </div>
      <div className="h-1.5 overflow-hidden rounded-full bg-[#2a2638]">
        <div className={`h-full rounded-full ${barClass}`} style={{ width: `${percent}%` }} />
      </div>
      <p className="mt-1 text-right text-[10px] font-semibold text-[#8f8a9a]">{percent.toFixed(1)}%</p>
    </div>
  );
}

function calculatePercent(value: number, total: number) {
  if (total <= 0) {
    return 0;
  }

  return Math.min(100, Math.max(0, value / total * 100));
}

function formatAssetTime(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return assetTimeFormatter.format(date);
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
