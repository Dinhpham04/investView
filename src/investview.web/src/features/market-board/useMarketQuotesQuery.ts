import { useQuery } from '@tanstack/react-query';
import { getMarketQuotes } from '../../shared/api/marketApi';

export function useMarketQuotesQuery(boardId = 'G1') {
  return useQuery({
    queryKey: ['market-quotes', boardId],
    queryFn: () => getMarketQuotes({ boardId }),
  });
}
