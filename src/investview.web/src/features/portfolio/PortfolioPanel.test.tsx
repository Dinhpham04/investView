import { fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PortfolioPanel } from './PortfolioPanel';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';

describe('PortfolioPanel', () => {
  afterEach(() => {
    window.localStorage?.clear();
    vi.unstubAllGlobals();
  });

  it('logs in and renders simulated portfolio totals and recent orders', async () => {
    const fetchMock = vi.fn(createPortfolioFetch());
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<PortfolioPanel />);

    fireEvent.click(screen.getByRole('button', { name: 'Dang nhap demo' }));

    expect(await screen.findByText('97,085,000 VND')).toBeInTheDocument();
    expect(screen.getByText('2,915,000 VND')).toBeInTheDocument();
    expect(screen.getByText('100,000,000 VND')).toBeInTheDocument();
    expect(screen.getByText('HPG Buy 100 @ 29,150.00 Filled')).toBeInTheDocument();
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/portfolio',
        expect.objectContaining({
          headers: expect.objectContaining({ Authorization: 'Bearer test-token' }),
        }),
      ),
    );
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/orders',
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer test-token' }),
      }),
    );
  });
});

function createPortfolioFetch() {
  return (input: RequestInfo | URL, init?: RequestInit) => {
    const url = input.toString();
    const method = init?.method ?? 'GET';

    if (url === '/api/auth/demo-login' && method === 'POST') {
      return Promise.resolve(jsonResponse({
        accessToken: 'test-token',
        tokenType: 'Bearer',
        expiresAt: '2027-07-12T10:00:00Z',
        user: {
          id: '6b4e73e5-53f6-421c-bcec-24c2dfbcd4a5',
          email: 'demo@investview.local',
          displayName: 'InvestView Demo',
        },
      }));
    }

    if (url === '/api/portfolio') {
      return Promise.resolve(jsonResponse({
        cashAccounts: [{
          currency: 'VND',
          balance: 97_085_000,
          availableBalance: 97_085_000,
          updatedAt: '2026-07-12T09:00:00Z',
        }],
        holdings: [{
          symbol: 'HPG',
          boardId: 'G1',
          quantity: 100,
          availableQuantity: 100,
          averageCost: 29_150,
          lastPrice: 29_150,
          marketValue: 2_915_000,
          costValue: 2_915_000,
          unrealizedPnL: 0,
          updatedAt: '2026-07-12T09:00:00Z',
        }],
        totalCash: 97_085_000,
        totalAvailableCash: 97_085_000,
        totalMarketValue: 2_915_000,
        totalEquity: 100_000_000,
        totalUnrealizedPnL: 0,
        updatedAt: '2026-07-12T09:00:00Z',
      }));
    }

    if (url === '/api/orders') {
      return Promise.resolve(jsonResponse([{
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
        executions: [],
      }]));
    }

    return Promise.resolve(new Response('Not found', { status: 404, statusText: 'Not Found' }));
  };
}

function jsonResponse(body: unknown, init?: ResponseInit) {
  return new Response(JSON.stringify(body), {
    status: init?.status ?? 200,
    statusText: init?.statusText,
    headers: { 'Content-Type': 'application/json' },
  });
}
