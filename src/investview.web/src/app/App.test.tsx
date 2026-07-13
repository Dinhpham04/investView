import { act, render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import type { QuoteHubConnectionStatus } from '../shared/realtime/useQuoteHubConnection';

const testRuntime = vi.hoisted(() => ({
  onConnectionStatusChange: null as ((status: QuoteHubConnectionStatus) => void) | null,
}));

vi.mock('ag-grid-react', () => ({
  AgGridProvider: ({ children }: { children: ReactNode }) => children,
}));

vi.mock('../features/market-board/MarketBoard', () => ({
  MarketBoard: ({
    onConnectionStatusChange,
  }: {
    onConnectionStatusChange?: (status: QuoteHubConnectionStatus) => void;
  }) => {
    testRuntime.onConnectionStatusChange = onConnectionStatusChange ?? null;

    return <div data-testid="market-board" />;
  },
}));

vi.mock('../features/auth/DemoSessionControls', () => ({
  DemoSessionControls: () => <button type="button">Demo login</button>,
}));

describe('App header', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-07-13T02:03:04.000Z'));
    testRuntime.onConnectionStatusChange = null;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders the market clock, websocket status, and demo login in the right header controls', () => {
    render(<App />);

    expect(screen.getByText('InvestView')).toBeInTheDocument();
    expect(screen.getByText('09:03:04')).toBeInTheDocument();
    expect(screen.getByRole('status', { name: 'WebSocket connecting' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /demo/i })).toBeInTheDocument();
    expect(screen.queryByText('API')).not.toBeInTheDocument();
    expect(screen.queryByText('Local demo')).not.toBeInTheDocument();
    expect(screen.queryByText('Simulated trading')).not.toBeInTheDocument();

    act(() => {
      testRuntime.onConnectionStatusChange?.('connected');
    });

    expect(screen.getByRole('status', { name: 'WebSocket connected' })).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(1_000);
    });

    expect(screen.getByText('09:03:05')).toBeInTheDocument();
  });
});
