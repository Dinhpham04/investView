import { fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { WatchlistPanel } from './WatchlistPanel';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';

describe('WatchlistPanel', () => {
  afterEach(() => {
    window.localStorage?.clear();
    vi.unstubAllGlobals();
  });

  it('logs in with the demo account and manages watchlist symbols', async () => {
    const fetchMock = vi.fn(createWatchlistFetch());
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<WatchlistPanel />);

    fireEvent.click(screen.getByRole('button', { name: 'Danh muc cua toi' }));
    fireEvent.click(screen.getByRole('button', { name: 'Dang nhap demo' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith('/api/auth/demo-login', expect.objectContaining({ method: 'POST' })),
    );
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/watchlist',
        expect.objectContaining({
          headers: expect.objectContaining({ Authorization: 'Bearer test-token' }),
        }),
      ),
    );

    fireEvent.change(screen.getByLabelText('Ma CK'), { target: { value: 'hpg' } });
    fireEvent.click(screen.getByRole('button', { name: 'Them' }));

    expect(await screen.findByText('HPG')).toBeInTheDocument();
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/watchlist',
        expect.objectContaining({
          body: JSON.stringify({ symbol: 'hpg', boardId: 'G1' }),
          method: 'POST',
        }),
      ),
    );

    fireEvent.click(screen.getByRole('button', { name: 'Xoa HPG G1' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/watchlist/G1/HPG',
        expect.objectContaining({ method: 'DELETE' }),
      ),
    );
    await waitFor(() => expect(screen.queryByText('HPG')).not.toBeInTheDocument());
  });
});

function createWatchlistFetch() {
  let items: Array<{ id: string; symbol: string; boardId: string; createdAt: string }> = [];

  return (input: RequestInfo | URL, init?: RequestInit) => {
    const url = input.toString();
    const method = init?.method ?? 'GET';

    if (url === '/api/auth/demo-login' && method === 'POST') {
      return Promise.resolve(jsonResponse({
        accessToken: 'test-token',
        tokenType: 'Bearer',
        expiresAt: '2026-07-12T10:00:00Z',
        user: {
          id: '6b4e73e5-53f6-421c-bcec-24c2dfbcd4a5',
          email: 'demo@investview.local',
          displayName: 'InvestView Demo',
        },
      }));
    }

    if (url === '/api/watchlist' && method === 'GET') {
      return Promise.resolve(jsonResponse(items));
    }

    if (url === '/api/watchlist' && method === 'POST') {
      const body = JSON.parse(String(init?.body));
      const item = {
        id: '0a8f1b4d-37e8-4d0e-9e5e-a0e8d28f86c1',
        symbol: String(body.symbol).trim().toUpperCase(),
        boardId: String(body.boardId).trim().toUpperCase(),
        createdAt: '2026-07-12T09:00:00Z',
      };
      items = [item];
      return Promise.resolve(jsonResponse(item, { status: 201 }));
    }

    if (url === '/api/watchlist/G1/HPG' && method === 'DELETE') {
      items = [];
      return Promise.resolve(new Response(null, { status: 204 }));
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
