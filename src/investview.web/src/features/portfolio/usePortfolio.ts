import { useQuery } from '@tanstack/react-query';
import { getOrders, getPortfolio } from '../../shared/api/tradingApi';
import { useDemoSession } from '../auth/useDemoSession';

export function usePortfolio() {
  const { session } = useDemoSession();
  const accessToken = session?.accessToken ?? null;

  const portfolioQuery = useQuery({
    queryKey: ['portfolio', accessToken],
    queryFn: () => getPortfolio(accessToken ?? ''),
    enabled: accessToken != null,
  });

  const ordersQuery = useQuery({
    queryKey: ['orders', accessToken],
    queryFn: () => getOrders(accessToken ?? ''),
    enabled: accessToken != null,
  });

  return {
    accessToken,
    orders: ordersQuery.data ?? [],
    ordersQuery,
    portfolio: portfolioQuery.data ?? null,
    portfolioQuery,
  };
}
