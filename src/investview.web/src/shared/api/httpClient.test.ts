import { afterEach, describe, expect, it, vi } from 'vitest';
import { getJson, subscribeToUnauthorized } from './httpClient';

describe('httpClient authorization failures', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('preserves the response status and notifies unauthorized subscribers', async () => {
    const onUnauthorized = vi.fn();
    const unsubscribe = subscribeToUnauthorized(onUnauthorized);
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(
      new Response(null, { status: 401, statusText: 'Unauthorized' }),
    )));

    const request = getJson('/api/protected');

    await expect(request).rejects.toMatchObject({
      status: 401,
      statusText: 'Unauthorized',
    });
    expect(onUnauthorized).toHaveBeenCalledOnce();

    unsubscribe();
  });
});
