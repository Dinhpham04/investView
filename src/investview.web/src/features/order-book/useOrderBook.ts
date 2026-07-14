import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { cancelOrder, getOrders } from '../../shared/api/tradingApi';
import type { SimulatedOrder } from '../../shared/types/trading';
import { useDemoSession } from '../auth/useDemoSession';

export function useOrderBook() {
  const queryClient = useQueryClient();
  const { session, status } = useDemoSession();
  const accessToken = session?.accessToken ?? null;

  const ordersQuery = useQuery({
    queryKey: ['orders', accessToken],
    queryFn: () => getOrders(accessToken ?? ''),
    enabled: accessToken != null,
  });

  const cancelOrderMutation = useMutation({
    mutationFn: (order: SimulatedOrder) => {
      if (accessToken == null) {
        throw new Error('Bạn cần đăng nhập tài khoản demo.');
      }

      return cancelOrder(accessToken, order.id);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['orders'] });
      void queryClient.invalidateQueries({ queryKey: ['portfolio'] });
      void queryClient.invalidateQueries({ queryKey: ['portfolio-holdings'] });
    },
  });

  return {
    cancelOrderMutation,
    orders: ordersQuery.data ?? [],
    ordersQuery,
    sessionStatus: status,
  };
}
