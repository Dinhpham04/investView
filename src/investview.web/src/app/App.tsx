import { AllCommunityModule } from 'ag-grid-community';
import { AgGridProvider } from 'ag-grid-react';
import { MarketBoard } from '../features/market-board/MarketBoard';
import { PortfolioPanel } from '../features/portfolio/PortfolioPanel';
import { SystemStatusIndicator } from '../features/system-status/SystemStatus';

const agGridModules = [AllCommunityModule];

export function App() {
  return (
    <AgGridProvider modules={agGridModules}>
      <main className="min-h-svh bg-market-bg text-market-text">
        <header className="flex min-h-11 flex-wrap items-center justify-between gap-3 border-b border-market-border bg-market-surface px-4 py-2">
          <div>
            <p className="text-[11px] font-bold uppercase text-market-text-muted">InvestView</p>
          </div>
          <div className="flex items-center gap-2 text-[11px] font-semibold">
            <SystemStatusIndicator />
            <span className="rounded-sm border border-market-border bg-market-surface-2 px-2 py-1 text-state-warning">
              Local demo
            </span>
            <span className="rounded-sm border border-market-border bg-market-surface-2 px-2 py-1 text-market-text-muted">
              Simulated trading
            </span>
          </div>
        </header>

        <section className="grid min-h-[calc(100svh-45px)] grid-cols-1" aria-label="InvestView workspace">
          <PortfolioPanel />
          <MarketBoard />
        </section>
      </main>
    </AgGridProvider>
  );
}
