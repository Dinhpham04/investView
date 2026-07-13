import { act, render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useQuoteHubConnection, type SymbolOhlcSubscription } from './useQuoteHubConnection';
import type { MarketOhlcUpdate, MarketQuoteUpdate, MarketSessionUpdate } from '../types/market';

type FakeConnection = {
  invoke: ReturnType<typeof vi.fn>;
  off: ReturnType<typeof vi.fn>;
  on: ReturnType<typeof vi.fn>;
  onclose: ReturnType<typeof vi.fn>;
  onreconnected: ReturnType<typeof vi.fn>;
  onreconnecting: ReturnType<typeof vi.fn>;
  start: ReturnType<typeof vi.fn>;
  stop: ReturnType<typeof vi.fn>;
};

const runtime = vi.hoisted(() => ({
  connection: null as FakeConnection | null,
  handlers: new Map<string, (payload: unknown) => void>(),
  invoke: vi.fn(),
  start: vi.fn(() => Promise.resolve()),
  stop: vi.fn(() => Promise.resolve()),
}));

vi.mock('./quoteHubClient', () => ({
  createQuoteHubConnection: vi.fn(() => {
    const connection = {
      invoke: runtime.invoke,
      off: vi.fn(),
      on: vi.fn((eventName: string, handler: (payload: unknown) => void) => {
        runtime.handlers.set(eventName, handler);
      }),
      onclose: vi.fn(),
      onreconnected: vi.fn(),
      onreconnecting: vi.fn(),
      start: runtime.start,
      stop: runtime.stop,
    };
    runtime.connection = connection;

    return connection;
  }),
  quoteHubPath: '/hubs/quotes',
}));

describe('useQuoteHubConnection', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    runtime.connection = null;
    runtime.handlers.clear();
    runtime.invoke.mockReset();
    runtime.start.mockReset();
    runtime.start.mockResolvedValue(undefined);
    runtime.stop.mockReset();
    runtime.stop.mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('sends symbol OHLC subscription after the connection starts', async () => {
    render(<Harness symbolOhlcSubscription={{ resolutions: ['1D'], symbol: 'HPG' }} />);

    await startConnection();

    expect(runtime.invoke).toHaveBeenCalledWith('SubscribeMarketBoard', {
      boardId: 'G1',
      symbols: ['HPG'],
    });
    expect(runtime.invoke).toHaveBeenCalledWith('SubscribeSymbolOhlc', {
      resolutions: ['1D'],
      symbol: 'HPG',
    });
  });

  it('clears symbol OHLC subscription when the chart demand is removed', async () => {
    const view = render(<Harness symbolOhlcSubscription={{ resolutions: ['1D'], symbol: 'HPG' }} />);
    await startConnection();
    runtime.invoke.mockClear();

    await act(async () => {
      view.rerender(<Harness symbolOhlcSubscription={null} />);
    });

    expect(runtime.invoke).toHaveBeenCalledWith('SubscribeSymbolOhlc', {
      resolutions: [],
      symbol: null,
    });
  });

  it('forwards realtime OHLC updates from SignalR', async () => {
    const onOhlcUpdate = vi.fn();
    render(<Harness onOhlcUpdate={onOhlcUpdate} symbolOhlcSubscription={null} />);
    await startConnection();

    runtime.handlers.get('ReceiveOhlcUpdate')?.({
      close: 1834,
      high: 1835,
      isClosed: false,
      low: 1830,
      open: 1831,
      resolution: '1',
      symbol: 'VNINDEX',
      time: '2026-07-13T03:00:00.000Z',
      type: 'INDEX',
      updatedAt: '2026-07-13T03:00:05.000Z',
      volume: 1000,
    });

    expect(onOhlcUpdate).toHaveBeenCalledWith(expect.objectContaining({
      close: 1834,
      symbol: 'VNINDEX',
      type: 'INDEX',
    }));
  });

  it('forwards market session updates from SignalR', async () => {
    const onMarketSessionUpdate = vi.fn();
    render(<Harness onMarketSessionUpdate={onMarketSessionUpdate} symbolOhlcSubscription={null} />);
    await startConnection();

    runtime.handlers.get('ReceiveMarketSessionUpdate')?.({
      boardId: 'G1',
      eventId: 'AB2',
      isAfterHours: false,
      isAuction: false,
      isContinuous: true,
      isOpen: true,
      isPutThrough: false,
      label: 'Liên tục',
      marketId: 'HOSE',
      phase: 'CONTINUOUS',
      productGroupId: 'STO',
      source: 'REALTIME',
      tradingSessionId: '40',
      updatedAt: '2026-07-13T02:20:00.000Z',
    });

    expect(onMarketSessionUpdate).toHaveBeenCalledWith(expect.objectContaining({
      boardId: 'G1',
      phase: 'CONTINUOUS',
      source: 'REALTIME',
    }));
  });
});

function Harness({
  onMarketSessionUpdate,
  onOhlcUpdate,
  symbolOhlcSubscription,
}: {
  onMarketSessionUpdate?: (update: MarketSessionUpdate) => void;
  onOhlcUpdate?: (update: MarketOhlcUpdate) => void;
  symbolOhlcSubscription: SymbolOhlcSubscription | null;
}) {
  useQuoteHubConnection({
    marketBoardSubscription: {
      boardId: 'G1',
      symbols: ['HPG'],
    },
    onMarketSessionUpdate,
    onOhlcUpdate,
    onQuoteUpdate: (_update: MarketQuoteUpdate) => {},
    symbolOhlcSubscription,
  });

  return null;
}

async function startConnection() {
  await act(async () => {
    await vi.runOnlyPendingTimersAsync();
  });
}
