import { useHealthQuery } from './useHealthQuery';

export function SystemStatus() {
  const healthQuery = useHealthQuery();

  if (healthQuery.isPending) {
    return (
      <section className="status-panel" aria-busy="true">
        <div className="panel-heading">
          <h2>System status</h2>
          <span className="status-chip status-chip--pending">Checking</span>
        </div>
        <p className="muted-text">Connecting to API...</p>
      </section>
    );
  }

  if (healthQuery.isError) {
    return (
      <section className="status-panel">
        <div className="panel-heading">
          <h2>System status</h2>
          <span className="status-chip status-chip--down">Offline</span>
        </div>
        <p className="error-text">{healthQuery.error.message}</p>
      </section>
    );
  }

  return (
    <section className="status-panel">
      <div className="panel-heading">
        <h2>System status</h2>
        <span className="status-chip status-chip--up">{healthQuery.data.status}</span>
      </div>
      <dl className="status-list">
        <div>
          <dt>Service</dt>
          <dd>{healthQuery.data.service}</dd>
        </div>
        <div>
          <dt>API</dt>
          <dd>Connected</dd>
        </div>
      </dl>
    </section>
  );
}
