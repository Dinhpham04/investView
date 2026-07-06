import { useHealthQuery } from './useHealthQuery';

export function SystemStatusIndicator() {
  const healthQuery = useHealthQuery();

  const status = healthQuery.isPending ? 'checking' : healthQuery.isError ? 'offline' : 'online';
  const dotClass = status === 'online' ? 'bg-state-online' : status === 'offline' ? 'bg-state-error' : 'bg-state-warning';
  const label = status === 'online' ? 'API online' : status === 'offline' ? 'API offline' : 'API checking';

  return (
    <span
      aria-label={label}
      className="inline-flex h-7 items-center gap-2 rounded-sm border border-market-border bg-market-surface-2 px-2 text-market-text-muted"
      role="status"
      title={label}
    >
      <span className={`h-2.5 w-2.5 rounded-full ${dotClass}`} aria-hidden="true" />
      <span className="text-[11px] font-semibold">API</span>
    </span>
  );
}
