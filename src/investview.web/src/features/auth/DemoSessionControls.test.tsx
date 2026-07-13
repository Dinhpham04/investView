import { fireEvent, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';
import { DemoSessionControls } from './DemoSessionControls';

describe('DemoSessionControls', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', createMemoryStorage());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('logs in and logs out from the app header controls', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(jsonResponse({
      accessToken: 'test-token',
      tokenType: 'Bearer',
      expiresAt: '2027-07-12T10:00:00Z',
      user: {
        id: '6b4e73e5-53f6-421c-bcec-24c2dfbcd4a5',
        email: 'demo@investview.local',
        displayName: 'InvestView Demo',
      },
    }))));

    renderWithQueryClient(<DemoSessionControls />);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));

    expect(await screen.findByText('InvestView Demo')).toBeInTheDocument();
    expect(window.localStorage.getItem('investview.demoSession')).not.toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Đăng xuất' }));

    expect(screen.getByRole('button', { name: 'Đăng nhập demo' })).toBeInTheDocument();
    expect(window.localStorage.getItem('investview.demoSession')).toBeNull();
  });
});

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
