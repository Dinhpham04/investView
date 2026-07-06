import { screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SystemStatusIndicator } from './SystemStatus';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';

describe('SystemStatusIndicator', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders a compact online API status when the API is reachable', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ status: 'Healthy', service: 'InvestView.Api' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    );

    renderWithQueryClient(<SystemStatusIndicator />);

    expect(screen.getByRole('status', { name: 'API checking' })).toBeInTheDocument();
    expect(await screen.findByRole('status', { name: 'API online' })).toBeInTheDocument();
    expect(screen.getByText('API')).toBeInTheDocument();
  });

  it('renders a compact offline API status when the API request fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response('Service unavailable', {
          status: 503,
          statusText: 'Service Unavailable',
        }),
      ),
    );

    renderWithQueryClient(<SystemStatusIndicator />);

    expect(await screen.findByRole('status', { name: 'API offline' })).toBeInTheDocument();
  });
});
