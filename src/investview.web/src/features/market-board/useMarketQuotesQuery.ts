import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getMarketQuotes, type GetMarketQuotesParams } from '../../shared/api/marketApi';

export function useMarketQuotesQuery(params: GetMarketQuotesParams = { boardId: 'G1' }) {
  return useQuery({
    placeholderData: keepPreviousData,
    queryKey: ['market-quotes', params],
    queryFn: () => getMarketQuotes(params),
  });
}
