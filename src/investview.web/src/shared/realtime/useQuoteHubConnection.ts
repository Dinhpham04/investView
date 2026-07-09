import { useEffect, useRef, useState } from 'react';
import { createQuoteHubConnection, quoteHubPath } from './quoteHubClient';
import type { MarketQuoteUpdate, MarketTradeUpdate, QuoteStreamStatus } from '../types/market';

export type QuoteHubConnectionStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'error';

export type UseQuoteHubConnectionOptions = {
  enabled?: boolean;
  hubUrl?: string;
  marketBoardSubscription?: MarketBoardSubscription | null;
  onQuoteUpdate: (update: MarketQuoteUpdate) => void;
  onTradeUpdate?: (update: MarketTradeUpdate) => void;
  onStreamStatus?: (status: QuoteStreamStatus) => void;
};

export type MarketBoardSubscription = {
  boardId: string;
  symbols: string[];
};

export type QuoteHubConnectionState = {
  status: QuoteHubConnectionStatus;
  lastError: string | null;
};

export function useQuoteHubConnection({
  enabled = true,
  hubUrl = quoteHubPath,
  marketBoardSubscription = null,
  onQuoteUpdate,
  onTradeUpdate,
  onStreamStatus,
}: UseQuoteHubConnectionOptions): QuoteHubConnectionState {
  const quoteUpdateRef = useRef(onQuoteUpdate);
  const tradeUpdateRef = useRef(onTradeUpdate);
  const streamStatusRef = useRef(onStreamStatus);
  const connectionRef = useRef<ReturnType<typeof createQuoteHubConnection> | null>(null);
  const marketBoardSubscriptionRef = useRef(marketBoardSubscription);
  const [connectionState, setConnectionState] = useState<QuoteHubConnectionState>({
    status: enabled ? 'connecting' : 'idle',
    lastError: null,
  });

  useEffect(() => {
    quoteUpdateRef.current = onQuoteUpdate;
  }, [onQuoteUpdate]);

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

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}
