import { useQuery } from '@tanstack/react-query';
import { getMarketSession, type GetMarketSessionParams } from '../../shared/api/marketApi';

const marketSessionRefetchIntervalMs = 30_000;

export function useMarketSessionQuery(params: GetMarketSessionParams = {}) {
  return useQuery({
    queryKey: ['market-session', params.productGroupId ?? 'STO', params.boardId ?? 'G1', params.marketId ?? 'HOSE'],
    queryFn: () => getMarketSession(params),
    refetchInterval: marketSessionRefetchIntervalMs,
    refetchOnReconnect: true,
    refetchOnWindowFocus: true,
    staleTime: marketSessionRefetchIntervalMs,
  });
}
