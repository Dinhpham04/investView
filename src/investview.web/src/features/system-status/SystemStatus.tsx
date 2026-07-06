import { useHealthQuery } from './useHealthQuery';

export function SystemStatus() {
  const healthQuery = useHealthQuery();

  if (healthQuery.isPending) {
    return (
      <section className="border border-market-border bg-market-surface p-4" aria-busy="true">
        <div className="mb-4 flex items-center justify-between gap-3">
          <h2 className="text-sm font-bold text-market-text">System status</h2>
          <span className="rounded-sm border border-market-border px-2 py-1 text-xs font-bold text-state-warning">
            Checking
          </span>
        </div>
        <p className="text-sm text-market-text-muted">Connecting to API...</p>
      </section>
    );
  }

  if (healthQuery.isError) {
    return (
      <section className="border border-market-border bg-market-surface p-4">
        <div className="mb-4 flex items-center justify-between gap-3">
          <h2 className="text-sm font-bold text-market-text">System status</h2>
          <span className="rounded-sm border border-market-border px-2 py-1 text-xs font-bold text-state-error">
            Offline
          </span>
        </div>
        <p className="text-sm font-semibold text-state-error">{healthQuery.error.message}</p>
      </section>
    );
  }

  return (
    <section className="border border-market-border bg-market-surface p-4">
      <div className="mb-4 flex items-center justify-between gap-3">
        <h2 className="text-sm font-bold text-market-text">System status</h2>
        <span className="rounded-sm border border-market-border px-2 py-1 text-xs font-bold text-state-online">
          {healthQuery.data.status}
        </span>
      </div>
      <dl className="grid gap-3">
        <div className="flex items-baseline justify-between gap-3 border-t border-market-border pt-3">
          <dt className="text-sm text-market-text-muted">Service</dt>
          <dd className="m-0 text-right text-sm font-bold text-market-text">{healthQuery.data.service}</dd>
        </div>
        <div className="flex items-baseline justify-between gap-3 border-t border-market-border pt-3">
          <dt className="text-sm text-market-text-muted">API</dt>
          <dd className="m-0 text-right text-sm font-bold text-market-text">Connected</dd>
        </div>
      </dl>
    </section>
  );
}
