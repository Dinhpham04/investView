import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getMarketQuotes, type GetMarketQuotesParams } from '../../shared/api/marketApi';

export function useMarketQuotesQuery(
  params: GetMarketQuotesParams = { boardId: 'G1' },
  options: { enabled?: boolean } = {},
) {
  return useQuery({
    enabled: options.enabled ?? true,
    placeholderData: keepPreviousData,
    queryKey: ['market-quotes', params],
    queryFn: () => getMarketQuotes(params),
  });
}
