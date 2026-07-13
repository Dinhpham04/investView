import { useEffect, useRef, useState } from 'react';
import { createQuoteHubConnection, quoteHubPath } from './quoteHubClient';
import type { MarketIndexUpdate, MarketOhlcUpdate, MarketQuoteUpdate, MarketSessionUpdate, MarketTradeUpdate, QuoteStreamStatus } from '../types/market';

export type QuoteHubConnectionStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'error';

export type UseQuoteHubConnectionOptions = {
  enabled?: boolean;
  hubUrl?: string;
  marketBoardSubscription?: MarketBoardSubscription | null;
  symbolOhlcSubscription?: SymbolOhlcSubscription | null;
  onMarketIndexUpdate?: (update: MarketIndexUpdate) => void;
  onMarketSessionUpdate?: (update: MarketSessionUpdate) => void;
  onOhlcUpdate?: (update: MarketOhlcUpdate) => void;
  onQuoteUpdate: (update: MarketQuoteUpdate) => void;
  onTradeUpdate?: (update: MarketTradeUpdate) => void;
  onStreamStatus?: (status: QuoteStreamStatus) => void;
};

export type MarketBoardSubscription = {
  boardId: string;
  symbols: string[];
};

export type SymbolOhlcSubscription = {
  resolutions: string[];
  symbol: string;
};

export type QuoteHubConnectionState = {
  status: QuoteHubConnectionStatus;
  lastError: string | null;
};

export function useQuoteHubConnection({
  enabled = true,
  hubUrl = quoteHubPath,
  marketBoardSubscription = null,
  symbolOhlcSubscription = null,
  onMarketIndexUpdate,
  onMarketSessionUpdate,
  onOhlcUpdate,
  onQuoteUpdate,
  onTradeUpdate,
  onStreamStatus,
}: UseQuoteHubConnectionOptions): QuoteHubConnectionState {
  const marketIndexUpdateRef = useRef(onMarketIndexUpdate);
  const marketSessionUpdateRef = useRef(onMarketSessionUpdate);
  const ohlcUpdateRef = useRef(onOhlcUpdate);
  const quoteUpdateRef = useRef(onQuoteUpdate);
  const tradeUpdateRef = useRef(onTradeUpdate);
  const streamStatusRef = useRef(onStreamStatus);
  const connectionRef = useRef<ReturnType<typeof createQuoteHubConnection> | null>(null);
  const marketBoardSubscriptionRef = useRef(marketBoardSubscription);
  const symbolOhlcSubscriptionRef = useRef(symbolOhlcSubscription);
  const [connectionState, setConnectionState] = useState<QuoteHubConnectionState>({
    status: enabled ? 'connecting' : 'idle',
    lastError: null,
  });

  useEffect(() => {
    quoteUpdateRef.current = onQuoteUpdate;
  }, [onQuoteUpdate]);

  useEffect(() => {
    marketIndexUpdateRef.current = onMarketIndexUpdate;
  }, [onMarketIndexUpdate]);

  useEffect(() => {
    marketSessionUpdateRef.current = onMarketSessionUpdate;
  }, [onMarketSessionUpdate]);

  useEffect(() => {
    ohlcUpdateRef.current = onOhlcUpdate;
  }, [onOhlcUpdate]);

  useEffect(() => {
    tradeUpdateRef.current = onTradeUpdate;
  }, [onTradeUpdate]);

  useEffect(() => {
    streamStatusRef.current = onStreamStatus;
  }, [onStreamStatus]);

  useEffect(() => {
    marketBoardSubscriptionRef.current = marketBoardSubscription;
  }, [marketBoardSubscription]);

  useEffect(() => {
    symbolOhlcSubscriptionRef.current = symbolOhlcSubscription;
  }, [symbolOhlcSubscription]);

  useEffect(() => {
    if (!enabled) {
      connectionRef.current = null;
      setConnectionState({ status: 'idle', lastError: null });
      return undefined;
    }

    let disposed = false;
    const connection = createQuoteHubConnection(hubUrl);
    connectionRef.current = connection;

    connection.on('ReceiveQuoteUpdate', (update: MarketQuoteUpdate) => {
      quoteUpdateRef.current(update);
    });

    connection.on('ReceiveMarketIndexUpdate', (update: MarketIndexUpdate) => {
      marketIndexUpdateRef.current?.(update);
    });

    connection.on('ReceiveMarketSessionUpdate', (update: MarketSessionUpdate) => {
      marketSessionUpdateRef.current?.(update);
    });

    connection.on('ReceiveOhlcUpdate', (update: MarketOhlcUpdate) => {
      ohlcUpdateRef.current?.(update);
    });

    connection.on('ReceiveTradeUpdate', (update: MarketTradeUpdate) => {
      tradeUpdateRef.current?.(update);
    });

    connection.on('ReceiveStreamStatus', (status: QuoteStreamStatus) => {
      streamStatusRef.current?.(status);
    });

    connection.onreconnecting((error) => {
      if (!disposed) {
        setConnectionState({ status: 'reconnecting', lastError: error?.message ?? null });
      }
    });

    connection.onreconnected(() => {
      if (!disposed) {
        setConnectionState({ status: 'connected', lastError: null });
        void sendMarketBoardSubscription(connection, marketBoardSubscriptionRef.current);
        void sendSymbolOhlcSubscription(connection, symbolOhlcSubscriptionRef.current);
      }
    });

    connection.onclose((error) => {
      if (!disposed) {
        setConnectionState({
          status: error ? 'error' : 'disconnected',
          lastError: error?.message ?? null,
        });
      }
    });

    setConnectionState({ status: 'connecting', lastError: null });

    const startTimer = window.setTimeout(() => {
      if (disposed) {
        return;
      }

      void connection.start().then(
        () => {
          if (disposed) {
            void connection.stop();
            return;
          }

          setConnectionState({ status: 'connected', lastError: null });
          void sendMarketBoardSubscription(connection, marketBoardSubscriptionRef.current);
          void sendSymbolOhlcSubscription(connection, symbolOhlcSubscriptionRef.current);
        },
        (error: unknown) => {
          if (!disposed) {
            setConnectionState({ status: 'error', lastError: getErrorMessage(error) });
          }
        },
      );
    }, 0);

    return () => {
      disposed = true;
      window.clearTimeout(startTimer);
      connection.off('ReceiveQuoteUpdate');
      connection.off('ReceiveMarketIndexUpdate');
      connection.off('ReceiveMarketSessionUpdate');
      connection.off('ReceiveOhlcUpdate');
      connection.off('ReceiveTradeUpdate');
      connection.off('ReceiveStreamStatus');
      if (connectionRef.current === connection) {
        connectionRef.current = null;
      }
      void connection.stop();
    };
  }, [enabled, hubUrl]);

  useEffect(() => {
    if (connectionState.status !== 'connected') {
      return;
    }

    void sendMarketBoardSubscription(connectionRef.current, marketBoardSubscription);
  }, [connectionState.status, marketBoardSubscription]);

  useEffect(() => {
    if (connectionState.status !== 'connected') {
      return;
    }

    void sendSymbolOhlcSubscription(connectionRef.current, symbolOhlcSubscription);
  }, [connectionState.status, symbolOhlcSubscription]);

  return connectionState;
}

async function sendMarketBoardSubscription(
  connection: ReturnType<typeof createQuoteHubConnection> | null,
  subscription: MarketBoardSubscription | null,
) {
  if (connection == null || subscription == null) {
    return;
  }

  try {
    await connection.invoke('SubscribeMarketBoard', {
      boardId: subscription.boardId,
      symbols: subscription.symbols,
    });
  } catch {
    // Connection state changes are handled by SignalR callbacks; subscription retries on reconnect.
  }
}

async function sendSymbolOhlcSubscription(
  connection: ReturnType<typeof createQuoteHubConnection> | null,
  subscription: SymbolOhlcSubscription | null,
) {
  if (connection == null) {
    return;
  }

  try {
    await connection.invoke('SubscribeSymbolOhlc', {
      resolutions: subscription?.resolutions ?? [],
      symbol: subscription?.symbol ?? null,
    });
  } catch {
    // Connection state changes are handled by SignalR callbacks; subscription retries on reconnect.
  }
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}
