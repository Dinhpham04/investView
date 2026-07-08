import { getJson } from './httpClient';
import type { MarketQuote, MarketTrade, OhlcBar, SymbolDetail } from '../types/market';

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

export type GetSymbolDetailParams = {
  boardId?: string;
  symbol: string;
};

export function getSymbolDetail(params: GetSymbolDetailParams) {
  const searchParams = new URLSearchParams();

  if (params.boardId) {
    searchParams.set('boardId', params.boardId);
  }

  const query = searchParams.toString();
  return getJson<SymbolDetail>(`/api/market/symbols/${encodeURIComponent(params.symbol)}${query ? `?${query}` : ''}`);
}

export type GetOhlcParams = {
  from?: string;
  resolution?: string;
  symbol: string;
  to?: string;
};

export function getOhlc(params: GetOhlcParams) {
  const searchParams = new URLSearchParams();

  if (params.resolution) {
    searchParams.set('resolution', params.resolution);
  }

  if (params.from) {
    searchParams.set('from', params.from);
  }

  if (params.to) {
    searchParams.set('to', params.to);
  }

  const query = searchParams.toString();
  return getJson<OhlcBar[]>(`/api/market/symbols/${encodeURIComponent(params.symbol)}/ohlc${query ? `?${query}` : ''}`);
}

export type GetLatestTradesParams = {
  boardId?: string;
  limit?: number;
  symbol: string;
};

export function getLatestTrades(params: GetLatestTradesParams) {
  const searchParams = new URLSearchParams();

  if (params.boardId) {
    searchParams.set('boardId', params.boardId);
  }

  if (params.limit != null) {
    searchParams.set('limit', params.limit.toString());
  }

  const query = searchParams.toString();
  return getJson<MarketTrade[]>(
    `/api/market/symbols/${encodeURIComponent(params.symbol)}/trades/latest${query ? `?${query}` : ''}`,
  );
}
