import { SystemStatus } from '../features/system-status/SystemStatus';

export function App() {
  return (
    <main className="app-shell">
      <header className="app-header">
        <div>
          <p className="eyebrow">InvestView</p>
          <h1>Market workstation</h1>
        </div>
        <span className="environment-badge">Local demo</span>
      </header>

      <section className="workspace-grid" aria-label="InvestView workspace">
        <SystemStatus />
      </section>
    </main>
  );
}
