import { fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { OrderTicketPanel } from './OrderTicketPanel';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';
import type { MarketQuote } from '../../shared/types/market';

describe('OrderTicketPanel', () => {
  afterEach(() => {
    window.localStorage?.clear();
    vi.unstubAllGlobals();
  });

  it('logs in and submits a simulated market order for the selected symbol', async () => {
    const fetchMock = vi.fn(createOrderTicketFetch());
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} />);

    fireEvent.click(screen.getByRole('button', { name: 'Dang nhap demo' }));
    await screen.findByRole('button', { name: 'Dat lenh mo phong' });
    fireEvent.change(screen.getByLabelText('Khoi luong'), { target: { value: '100' } });
    fireEvent.click(screen.getByRole('button', { name: 'Dat lenh mo phong' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/orders',
        expect.objectContaining({
          body: JSON.stringify({
            symbol: 'HPG',
            boardId: 'G1',
            side: 'Buy',
            quantity: 100,
            limitPrice: null,
          }),
          headers: expect.objectContaining({ Authorization: 'Bearer test-token' }),
          method: 'POST',
        }),
      ),
    );
    expect(await screen.findByText(/HPG Filled 100 @ 29,150.00/)).toBeInTheDocument();
  });

  it('disables submit when quantity is invalid', async () => {
    vi.stubGlobal('fetch', vi.fn(createOrderTicketFetch()));

    renderWithQueryClient(<OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} />);

    fireEvent.click(screen.getByRole('button', { name: 'Dang nhap demo' }));
    await screen.findByRole('button', { name: 'Dat lenh mo phong' });
    fireEvent.change(screen.getByLabelText('Khoi luong'), { target: { value: '0' } });

    expect(screen.getByText('Khoi luong phai la so nguyen duong.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Dat lenh mo phong' })).toBeDisabled();
  });
});

const quote: MarketQuote = {
  symbol: 'HPG',
  boardId: 'G1',
  marketId: 'HOSE',
  displayName: 'Hoa Phat Group',
  referencePrice: 28_600,
  ceilingPrice: 30_600,
  floorPrice: 26_600,
  lastPrice: 29_150,
  change: 550,
  changePercent: 1.92,
  lastQuantity: 2_500,
  totalVolume: 12_450_000,
  totalValue: 362_917_500_000,
  foreignBuyVolume: 786_100,
  foreignSellVolume: 1_227_649,
  foreignRoom: 1_742_502_798,
  openPrice: 28_700,
  highPrice: 29_200,
  lowPrice: 28_450,
  bidLevels: [],
  askLevels: [],
  tradingStatus: 'Continuous',
  updatedAt: '2026-07-03T07:45:00Z',
};

function createOrderTicketFetch() {
  return (input: RequestInfo | URL, init?: RequestInit) => {
    const url = input.toString();
    const method = init?.method ?? 'GET';

    if (url === '/api/auth/demo-login' && method === 'POST') {
      return Promise.resolve(jsonResponse(createDemoSession()));
    }

    if (url === '/api/orders' && method === 'POST') {
      return Promise.resolve(jsonResponse({
        id: 'c9fd5bb5-5bdb-4cd8-8ecb-3aeaf011dd40',
        symbol: 'HPG',
        boardId: 'G1',
        side: 'Buy',
        quantity: 100,
        limitPrice: null,
        status: 'Filled',
        filledQuantity: 100,
        averageFillPrice: 29_150,
        createdAt: '2026-07-12T09:00:00Z',
        updatedAt: '2026-07-12T09:00:00Z',
        executions: [{
          id: '2f059f94-a486-4513-a498-6fc262bf8331',
          quantity: 100,
          price: 29_150,
          grossAmount: 2_915_000,
          executedAt: '2026-07-12T09:00:00Z',
        }],
      }, { status: 201 }));
    }

    return Promise.resolve(new Response('Not found', { status: 404, statusText: 'Not Found' }));
  };
}

function createDemoSession() {
  return {
    accessToken: 'test-token',
    tokenType: 'Bearer',
    expiresAt: '2027-07-12T10:00:00Z',
    user: {
      id: '6b4e73e5-53f6-421c-bcec-24c2dfbcd4a5',
      email: 'demo@investview.local',
      displayName: 'InvestView Demo',
    },
  };
}

function jsonResponse(body: unknown, init?: ResponseInit) {
  return new Response(JSON.stringify(body), {
    status: init?.status ?? 200,
    statusText: init?.statusText,
    headers: { 'Content-Type': 'application/json' },
  });
}
