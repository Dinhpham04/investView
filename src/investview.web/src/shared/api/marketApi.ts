import { getJson } from './httpClient';
import type { MarketQuote } from '../types/market';

export type GetMarketQuotesParams = {
  boardId?: string;
  indexName?: string;
  marketId?: string;
  symbols?: string[];
};

export function getMarketQuotes(params: GetMarketQuotesParams = {}) {
  const searchParams = new URLSearchParams();

  if (params.boardId) {
    searchParams.set('boardId', params.boardId);
  }

  if (params.marketId) {
    searchParams.set('marketId', params.marketId);
  }

  if (params.indexName) {
    searchParams.set('indexName', params.indexName);
  }

  params.symbols?.forEach((symbol) => {
    searchParams.append('symbols', symbol);
  });

  const query = searchParams.toString();
  return getJson<MarketQuote[]>(`/api/market/quotes${query ? `?${query}` : ''}`);
}
