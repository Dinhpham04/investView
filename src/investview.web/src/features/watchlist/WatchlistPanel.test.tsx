import { fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { WatchlistPanel } from './WatchlistPanel';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';
import { DemoSessionControls } from '../auth/DemoSessionControls';

describe('WatchlistPanel', () => {
  afterEach(() => {
    window.localStorage?.clear();
    vi.unstubAllGlobals();
  });

  it('logs in with the demo account and creates/selects watchlist groups', async () => {
    const fetchMock = vi.fn(createWatchlistFetch());
    const onSelectGroup = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<><DemoSessionControls /><WatchlistPanel onSelectGroup={onSelectGroup} /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Danh mục của tôi' }));
    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));

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

    expect(await screen.findByText('Chưa có danh mục')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Tên danh mục'), { target: { value: 'TK H197731' } });
    fireEvent.click(screen.getByRole('button', { name: 'Tạo danh mục' }));

    const groupButton = await screen.findByRole('button', { name: /TK H197731/ });
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/watchlist',
        expect.objectContaining({
          body: JSON.stringify({ name: 'TK H197731' }),
          method: 'POST',
        }),
      ),
    );
    expect(onSelectGroup).toHaveBeenCalledWith(expect.objectContaining({ name: 'TK H197731' }));

    fireEvent.click(groupButton);
    expect(onSelectGroup).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'group-1', name: 'TK H197731' }));
  });

  it('keeps watchlist management gated behind the app-level login', () => {
    renderWithQueryClient(<WatchlistPanel />);
    fireEvent.click(screen.getByRole('button', { name: 'Danh mục của tôi' }));

    expect(screen.getByText('Đăng nhập ở góc trên bên phải để quản lý danh mục theo dõi.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tạo danh mục' })).not.toBeInTheDocument();
  });

  it('closes the dropdown when clicking outside', async () => {
    const fetchMock = vi.fn(createWatchlistFetch([{
      id: 'group-1',
      name: 'TK H197731',
      createdAt: '2026-07-12T09:00:00Z',
      updatedAt: '2026-07-12T09:00:00Z',
      items: [],
    }]));
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(
      <>
        <button type="button">Bên ngoài</button>
        <DemoSessionControls />
        <WatchlistPanel />
      </>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    fireEvent.click(screen.getByRole('button', { name: 'Danh mục của tôi' }));

    expect(await screen.findByRole('dialog', { name: 'Danh mục theo dõi' })).toBeInTheDocument();

    fireEvent.pointerDown(screen.getByRole('button', { name: 'Bên ngoài' }));

    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Danh mục theo dõi' })).not.toBeInTheDocument(),
    );
  });

  it('removes a symbol from the selected watchlist group', async () => {
    const fetchMock = vi.fn(createWatchlistFetch([{
      id: 'group-1',
      name: 'TK H197731',
      createdAt: '2026-07-12T09:00:00Z',
      updatedAt: '2026-07-12T09:00:00Z',
      items: [{
        id: 'item-1',
        groupId: 'group-1',
        symbol: 'HPG',
        boardId: 'G1',
        createdAt: '2026-07-12T09:01:00Z',
      }],
    }]));
    const onGroupChange = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(
      <>
        <DemoSessionControls />
        <WatchlistPanel selectedGroupId="group-1" onGroupChange={onGroupChange} />
      </>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    fireEvent.click(await screen.findByRole('button', { name: /TK H197731/ }));
    expect(await screen.findByText('HPG')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Xóa HPG khỏi TK H197731' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/watchlist/group-1/items/G1/HPG',
        expect.objectContaining({ method: 'DELETE' }),
      ),
    );
    await waitFor(() => expect(screen.queryByText('HPG')).not.toBeInTheDocument());
    expect(onGroupChange).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'group-1', items: [] }));
  });
});

type WatchlistGroupFixture = {
    id: string;
    name: string;
    createdAt: string;
    updatedAt: string;
    items: Array<{ id: string; groupId: string; symbol: string; boardId: string; createdAt: string }>;
  };

function createWatchlistFetch(initialGroups: WatchlistGroupFixture[] = []) {
  let groups = initialGroups;

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
      return Promise.resolve(jsonResponse(groups));
    }

    if (url === '/api/watchlist' && method === 'POST') {
      const body = JSON.parse(String(init?.body));
      const group = {
        id: 'group-1',
        name: String(body.name).trim(),
        createdAt: '2026-07-12T09:00:00Z',
        updatedAt: '2026-07-12T09:00:00Z',
        items: [],
      };
      groups = [group];
      return Promise.resolve(jsonResponse(group, { status: 201 }));
    }

    const deleteMatch = url.match(/^\/api\/watchlist\/([^/]+)\/items\/([^/]+)\/([^/]+)$/);
    if (deleteMatch && method === 'DELETE') {
      const [, groupId, boardId, symbol] = deleteMatch;
      groups = groups.map((group) =>
        group.id === groupId
          ? {
            ...group,
            items: group.items.filter((item) => item.boardId !== boardId || item.symbol !== symbol),
          }
          : group,
      );
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
