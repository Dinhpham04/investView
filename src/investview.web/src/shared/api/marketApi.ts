import { getJson } from './httpClient';
import type { MarketQuote } from '../types/market';

export type GetMarketQuotesParams = {
  boardId?: string;
  symbols?: string[];
};

export function getMarketQuotes(params: GetMarketQuotesParams = {}) {
  const searchParams = new URLSearchParams();

  if (params.boardId) {
    searchParams.set('boardId', params.boardId);
  }

  params.symbols?.forEach((symbol) => {
    searchParams.append('symbols', symbol);
  });

  const query = searchParams.toString();
  return getJson<MarketQuote[]>(`/api/market/quotes${query ? `?${query}` : ''}`);
}
