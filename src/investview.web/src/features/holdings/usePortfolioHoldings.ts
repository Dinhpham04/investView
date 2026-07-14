import { useQuery } from '@tanstack/react-query';
import { getPortfolioHoldings } from '../../shared/api/tradingApi';
import { useDemoSession } from '../auth/useDemoSession';

export function usePortfolioHoldings() {
  const { session, status } = useDemoSession();
  const accessToken = session?.accessToken ?? null;

  const holdingsQuery = useQuery({
    queryKey: ['portfolio-holdings', accessToken],
    queryFn: () => getPortfolioHoldings(accessToken ?? ''),
    enabled: accessToken != null,
  });

  return {
    accessToken,
    holdingsSnapshot: holdingsQuery.data ?? null,
    holdingsQuery,
    sessionStatus: status,
  };
}
