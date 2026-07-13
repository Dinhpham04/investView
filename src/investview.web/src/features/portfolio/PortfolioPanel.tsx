import { useDemoSession } from '../auth/useDemoSession';
import { formatMoney, formatOrderPrice, formatQuantity } from '../trading/tradingFormatters';
import { usePortfolio } from './usePortfolio';

export function PortfolioPanel() {
  const { session, status } = useDemoSession();
  const {
    orders,
    ordersQuery,
    portfolio,
    portfolioQuery,
  } = usePortfolio();
  const error = portfolioQuery.error ?? ordersQuery.error;
  const isPortfolioLoading = portfolioQuery.isPending;
  const latestOrders = orders.slice(0, 4);

  return (
    <section className="border-b border-market-border bg-market-surface px-3 py-2" aria-label="Tai khoan giao dich mo phong">
      <div className="flex flex-wrap items-center gap-3">
        <div className="min-w-44">
          <p className="text-[10px] font-bold uppercase tracking-normal text-state-warning">Simulated trading</p>
          <p className="text-[12px] font-semibold text-market-text">
            {session?.user.displayName ?? 'Tai khoan demo'}
          </p>
        </div>

        {status === 'checking' ? (
          <p className="text-[11px] font-semibold text-market-text-muted">Đang xác minh phiên đăng nhập...</p>
        ) : session == null ? (
          <p className="text-[11px] font-semibold text-market-text-muted">
            Đăng nhập ở góc trên bên phải để xem tài khoản mô phỏng.
          </p>
        ) : (
          <>
            <Metric label="Tien mat" value={formatPortfolioMetric(isPortfolioLoading, portfolio?.totalCash)} />
            <Metric label="Gia tri CK" value={formatPortfolioMetric(isPortfolioLoading, portfolio?.totalMarketValue)} />
            <Metric label="Tong tai san" value={formatPortfolioMetric(isPortfolioLoading, portfolio?.totalEquity)} tone="strong" />
            <Metric
              label="Lenh"
              value={ordersQuery.isPending ? 'Dang tai' : ordersQuery.isError ? '--' : `${orders.length} lenh`}
            />
          </>
        )}

        {latestOrders.length > 0 ? (
          <div className="ml-auto flex min-w-0 flex-wrap items-center gap-1.5 text-[11px]" aria-label="Lenh gan day">
            {latestOrders.map((order) => (
              <span
                className="rounded-sm border border-market-border bg-market-surface-2 px-2 py-1 font-semibold text-market-text-muted"
                key={order.id}
              >
                {order.symbol} {order.side} {formatQuantity(order.quantity)} @ {formatOrderPrice(order.averageFillPrice ?? order.limitPrice)} {order.status}
              </span>
            ))}
          </div>
        ) : null}
      </div>

      {error instanceof Error ? (
        <p className="mt-1 text-[11px] font-semibold text-state-error" role="alert">
          {error.message}
        </p>
      ) : null}
    </section>
  );
}

function formatPortfolioMetric(isLoading: boolean, value: number | null | undefined) {
  if (isLoading) {
    return 'Dang tai';
  }

  return value == null ? '--' : formatMoney(value);
}

function Metric({ label, value, tone = 'muted' }: { label: string; value: string; tone?: 'muted' | 'strong' }) {
  return (
    <div className="min-w-28 border-l border-market-border pl-3">
      <p className="text-[10px] font-bold uppercase tracking-normal text-market-text-subtle">{label}</p>
      <p className={`text-[12px] font-extrabold ${tone === 'strong' ? 'text-market-text' : 'text-market-text-muted'}`}>
        {value}
      </p>
    </div>
  );
}
