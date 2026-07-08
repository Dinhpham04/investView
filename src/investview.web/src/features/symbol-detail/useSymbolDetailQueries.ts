import { useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getLatestTrades, getOhlc, getSymbolDetail } from '../../shared/api/marketApi';

export type SymbolDetailSelection = {
  boardId: string;
  symbol: string;
};

export function useSymbolDetailQueries(selection: SymbolDetailSelection | null) {
  const selectionKey = selection == null ? '' : `${selection.boardId}:${selection.symbol}`;
  const previousSelectionKeyRef = useRef('');
  const chartRangeRef = useRef(createIntradayRange());
  if (previousSelectionKeyRef.current !== selectionKey) {
    previousSelectionKeyRef.current = selectionKey;
    chartRangeRef.current = createIntradayRange();
  }

  const chartRange = chartRangeRef.current;
  const enabled = selection != null;

  const detailQuery = useQuery({
    enabled,
    queryKey: ['symbol-detail', selection?.boardId, selection?.symbol],
    queryFn: () => getSymbolDetail({ boardId: selection!.boardId, symbol: selection!.symbol }),
  });

  const ohlcQuery = useQuery({
    enabled,
    queryKey: ['symbol-ohlc', selection?.symbol, chartRange.from, chartRange.to, '1'],
    queryFn: () =>
      getOhlc({
        from: chartRange.from,
        resolution: '1',
        symbol: selection!.symbol,
        to: chartRange.to,
      }),
  });

  const latestTradesQuery = useQuery({
    enabled,
    queryKey: ['symbol-latest-trades', selection?.boardId, selection?.symbol, 30],
    queryFn: () => getLatestTrades({ boardId: selection!.boardId, limit: 30, symbol: selection!.symbol }),
  });

  return {
    detailQuery,
    latestTradesQuery,
    ohlcQuery,
  };
}

function createIntradayRange() {
  const to = new Date();
  const from = new Date(to.getTime() - 24 * 60 * 60 * 1000);

  return {
    from: from.toISOString(),
    to: to.toISOString(),
  };
}
