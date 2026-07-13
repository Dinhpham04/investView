import { AllCommunityModule } from 'ag-grid-community';
import { AgGridProvider } from 'ag-grid-react';
import { useEffect, useState } from 'react';
import { DemoSessionControls } from '../features/auth/DemoSessionControls';
import { MarketBoard } from '../features/market-board/MarketBoard';
import type { QuoteHubConnectionStatus } from '../shared/realtime/useQuoteHubConnection';

const agGridModules = [AllCommunityModule];
const marketClockFormatter = new Intl.DateTimeFormat('vi-VN', {
  hour: '2-digit',
  hour12: false,
  minute: '2-digit',
  second: '2-digit',
  timeZone: 'Asia/Ho_Chi_Minh',
});

export function App() {
  const [connectionStatus, setConnectionStatus] = useState<QuoteHubConnectionStatus>('connecting');
  const [clockText, setClockText] = useState(() => formatMarketClock());

  useEffect(() => {
    const timerId = window.setInterval(() => {
      setClockText(formatMarketClock());
    }, 1_000);

    return () => window.clearInterval(timerId);
  }, []);

  return (
    <AgGridProvider modules={agGridModules}>
      <main className="min-h-svh bg-market-bg text-market-text">
        <header className="flex min-h-11 flex-wrap items-center justify-between gap-3 border-b border-market-border bg-market-surface px-4 py-2">
          <div>
            <p className="text-[11px] font-bold uppercase text-market-text-muted">InvestView</p>
          </div>
          <div className="flex items-center gap-2 text-[11px] font-semibold">
            <time className="tabular-nums text-market-text-muted" dateTime={clockText} title="Giờ giao dịch Việt Nam">
              {clockText}
            </time>
            <WebsocketStatusDot status={connectionStatus} />
            <DemoSessionControls />
          </div>
        </header>

        <section className="grid min-h-[calc(100svh-45px)] grid-cols-1" aria-label="InvestView workspace">
          <MarketBoard onConnectionStatusChange={setConnectionStatus} />
        </section>
      </main>
    </AgGridProvider>
  );
}

function WebsocketStatusDot({ status }: { status: QuoteHubConnectionStatus }) {
  const label = getWebsocketStatusLabel(status);

  return (
    <span
      aria-label={label}
      className={`h-2.5 w-2.5 rounded-full ${getWebsocketStatusDotClass(status)}`}
      role="status"
      title={label}
    />
  );
}

function formatMarketClock() {
  return marketClockFormatter.format(new Date());
}

function getWebsocketStatusLabel(status: QuoteHubConnectionStatus) {
  switch (status) {
    case 'connected':
      return 'WebSocket connected';
    case 'connecting':
      return 'WebSocket connecting';
    case 'reconnecting':
      return 'WebSocket reconnecting';
    case 'disconnected':
      return 'WebSocket disconnected';
    case 'error':
      return 'WebSocket offline';
    case 'idle':
      return 'WebSocket idle';
  }
}

function getWebsocketStatusDotClass(status: QuoteHubConnectionStatus) {
  switch (status) {
    case 'connected':
      return 'bg-state-online';
    case 'connecting':
    case 'reconnecting':
      return 'bg-state-warning';
    case 'disconnected':
    case 'error':
    case 'idle':
      return 'bg-state-error';
  }
}
