import { useEffect, useRef, useState } from 'react';
import { createQuoteHubConnection, quoteHubPath } from './quoteHubClient';
import type { MarketQuoteUpdate, QuoteStreamStatus } from '../types/market';

export type QuoteHubConnectionStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'error';

export type UseQuoteHubConnectionOptions = {
  enabled?: boolean;
  hubUrl?: string;
  onQuoteUpdate: (update: MarketQuoteUpdate) => void;
  onStreamStatus?: (status: QuoteStreamStatus) => void;
};

export type QuoteHubConnectionState = {
  status: QuoteHubConnectionStatus;
  lastError: string | null;
};

export function useQuoteHubConnection({
  enabled = true,
  hubUrl = quoteHubPath,
  onQuoteUpdate,
  onStreamStatus,
}: UseQuoteHubConnectionOptions): QuoteHubConnectionState {
  const quoteUpdateRef = useRef(onQuoteUpdate);
  const streamStatusRef = useRef(onStreamStatus);
  const [connectionState, setConnectionState] = useState<QuoteHubConnectionState>({
    status: enabled ? 'connecting' : 'idle',
    lastError: null,
  });

  useEffect(() => {
    quoteUpdateRef.current = onQuoteUpdate;
  }, [onQuoteUpdate]);

  useEffect(() => {
    streamStatusRef.current = onStreamStatus;
  }, [onStreamStatus]);

  useEffect(() => {
    if (!enabled) {
      setConnectionState({ status: 'idle', lastError: null });
      return undefined;
    }

    let disposed = false;
    const connection = createQuoteHubConnection(hubUrl);

    connection.on('ReceiveQuoteUpdate', (update: MarketQuoteUpdate) => {
      quoteUpdateRef.current(update);
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
      connection.off('ReceiveStreamStatus');
      void connection.stop();
    };
  }, [enabled, hubUrl]);

  return connectionState;
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : String(error);
}
