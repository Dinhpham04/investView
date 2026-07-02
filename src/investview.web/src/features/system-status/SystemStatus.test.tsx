import { screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SystemStatus } from './SystemStatus';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';

describe('SystemStatus', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders backend health when the API is reachable', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ status: 'Healthy', service: 'InvestView.Api' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    );

    renderWithQueryClient(<SystemStatus />);

    expect(screen.getByText('Checking')).toBeInTheDocument();
    expect(await screen.findByText('Healthy')).toBeInTheDocument();
    expect(screen.getByText('InvestView.Api')).toBeInTheDocument();
    expect(screen.getByText('Connected')).toBeInTheDocument();
  });

  it('renders an offline state when the API request fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response('Service unavailable', {
          status: 503,
          statusText: 'Service Unavailable',
        }),
      ),
    );

    renderWithQueryClient(<SystemStatus />);

    expect(await screen.findByText('Offline')).toBeInTheDocument();
    expect(screen.getByText('Request failed: 503 Service Unavailable')).toBeInTheDocument();
  });
});
