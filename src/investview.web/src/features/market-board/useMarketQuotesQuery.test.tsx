import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { PropsWithChildren } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useMarketQuotesQuery } from './useMarketQuotesQuery';
import type { MarketQuote } from '../../shared/types/market';

const marketApi = vi.hoisted(() => ({
  getMarketQuotes: vi.fn(),
}));

vi.mock('../../shared/api/marketApi', () => ({
  getMarketQuotes: marketApi.getMarketQuotes,
}));

describe('useMarketQuotesQuery', () => {
  afterEach(() => {
    marketApi.getMarketQuotes.mockReset();
  });

  it('keeps the previous quote list while a new market list is loading', async () => {
    const vn30Quotes = [createQuote('HPG')];
    let resolveHnx!: (quotes: MarketQuote[]) => void;
    marketApi.getMarketQuotes.mockImplementation((params: { indexName?: string }) => {
      if (params.indexName === 'VN30') {
        return Promise.resolve(vn30Quotes);
      }

      return new Promise<MarketQuote[]>((resolve) => {
        resolveHnx = resolve;
      });
    });
    const wrapper = createWrapper();

    const { result, rerender } = renderHook(
      ({ indexName }) => useMarketQuotesQuery({ boardId: 'G1', indexName }),
      {
        initialProps: { indexName: 'VN30' },
        wrapper,
      },
    );

    await waitFor(() => expect(result.current.data).toEqual(vn30Quotes));

    rerender({ indexName: 'HNX30' });

    expect(result.current.data).toEqual(vn30Quotes);
    expect(result.current.isPlaceholderData).toBe(true);

    resolveHnx([createQuote('SHS')]);
    await waitFor(() => expect(result.current.data?.map((quote) => quote.symbol)).toEqual(['SHS']));
  });
});

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  return function Wrapper({ children }: PropsWithChildren) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

function createQuote(symbol: string): MarketQuote {
  return {
    askLevels: [],
    bidLevels: [],
    boardId: 'G1',
    ceilingPrice: 10,
    change: 0,
    changePercent: 0,
    displayName: symbol,
    floorPrice: 8,
    foreignBuyVolume: null,
    foreignRoom: null,
    foreignSellVolume: null,
    highPrice: 9,
    lastPrice: 9,
    lastQuantity: 100,
    lowPrice: 9,
    marketId: 'HOSE',
    openPrice: 9,
    referencePrice: 9,
    symbol,
    totalValue: null,
    totalVolume: 1000,
    tradingStatus: 'OPEN',
    updatedAt: '2026-07-13T02:00:00.000Z',
  };
}
