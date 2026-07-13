import { act, fireEvent, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getJson } from '../../shared/api/httpClient';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';
import { useDemoSession } from './useDemoSession';

const storageKey = 'investview.demoSession';

describe('DemoSessionProvider', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', createMemoryStorage());
  });

  afterEach(() => {
    window.localStorage?.clear();
    vi.unstubAllGlobals();
  });

  it('does not expose a stored session until the API validates it', async () => {
    storeSession();
    let resolveProfile!: (response: Response) => void;
    const profileResponse = new Promise<Response>((resolve) => {
      resolveProfile = resolve;
    });
    const fetchMock = vi.fn(() => profileResponse);
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<SessionProbe />);

    expect(screen.getByText('checking:none')).toBeInTheDocument();
    await act(async () => {
      resolveProfile(jsonResponse({
        id: '6b4e73e5-53f6-421c-bcec-24c2dfbcd4a5',
        email: 'demo@investview.local',
        displayName: 'Verified Demo',
        cashAccounts: [],
      }));
    });

    expect(await screen.findByText('authenticated:Verified Demo')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/me',
      expect.objectContaining({
        headers: expect.objectContaining({ Authorization: 'Bearer stored-token' }),
      }),
    );
  });

  it('clears a stored session rejected by the API', async () => {
    storeSession();
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(
      new Response(null, { status: 401, statusText: 'Unauthorized' }),
    )));

    renderWithQueryClient(<SessionProbe />);

    expect(await screen.findByText('guest:none')).toBeInTheDocument();
    expect(window.localStorage.getItem(storageKey)).toBeNull();
  });

  it('returns an authenticated session to guest when a later request returns unauthorized', async () => {
    storeSession();
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => {
      if (input.toString() === '/api/me') {
        return Promise.resolve(jsonResponse({
          id: '6b4e73e5-53f6-421c-bcec-24c2dfbcd4a5',
          email: 'demo@investview.local',
          displayName: 'Verified Demo',
          cashAccounts: [],
        }));
      }

      return Promise.resolve(new Response(null, { status: 401, statusText: 'Unauthorized' }));
    }));

    renderWithQueryClient(<SessionProbe />);
    expect(await screen.findByText('authenticated:Verified Demo')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Call protected API' }));

    expect(await screen.findByText('guest:none')).toBeInTheDocument();
    expect(window.localStorage.getItem(storageKey)).toBeNull();
  });
});

function SessionProbe() {
  const { session, status } = useDemoSession();
  return (
    <>
      <div>{status}:{session?.user.displayName ?? 'none'}</div>
      <button
        type="button"
        onClick={() => {
          void getJson('/api/protected').catch(() => undefined);
        }}
      >
        Call protected API
      </button>
    </>
  );
}

function storeSession() {
  window.localStorage.setItem(storageKey, JSON.stringify({
    accessToken: 'stored-token',
    tokenType: 'Bearer',
    expiresAt: '2027-07-12T10:00:00Z',
    user: {
      id: '6b4e73e5-53f6-421c-bcec-24c2dfbcd4a5',
      email: 'demo@investview.local',
      displayName: 'Stored Demo',
    },
  }));
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });
}

function createMemoryStorage(): Storage {
  const values = new Map<string, string>();

  return {
    get length() {
      return values.size;
    },
    clear: () => values.clear(),
    getItem: (key) => values.get(key) ?? null,
    key: (index) => [...values.keys()][index] ?? null,
    removeItem: (key) => {
      values.delete(key);
    },
    setItem: (key, value) => {
      values.set(key, value);
    },
  };
}
