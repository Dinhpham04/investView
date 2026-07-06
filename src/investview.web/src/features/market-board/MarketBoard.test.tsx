import { screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MarketBoard } from './MarketBoard';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';
import type { MarketQuote } from '../../shared/types/market';

vi.mock('ag-grid-react', () => ({
  AgGridReact: ({
    columnDefs,
    rowData,
  }: {
    columnDefs: { headerName?: string; children?: { headerName?: string }[] }[];
    rowData: { symbol: string; lastPrice: number | null; totalVolume: number | null }[];
  }) => (
    <div role="grid">
      <div>
        {columnDefs.map((column) => (
          <span key={column.headerName}>{column.headerName}</span>
        ))}
      </div>
      {rowData.map((row) => (
        <div key={row.symbol} role="row">
          <span>{row.symbol}</span>
          <span>{row.lastPrice}</span>
          <span>{row.totalVolume}</span>
        </div>
      ))}
    </div>
  ),
}));

const quote: MarketQuote = {
  symbol: 'HPG',
  boardId: 'G1',
  marketId: 'HOSE',
  displayName: 'Hoa Phat Group',
  referencePrice: 27.4,
  ceilingPrice: 29.3,
  floorPrice: 25.5,
  lastPrice: 28.1,
  change: 0.7,
  changePercent: 2.55,
  lastQuantity: 18_000,
  totalVolume: 12_345_678,
  totalValue: 347_000_000_000,
  openPrice: 27.6,
  highPrice: 28.4,
  lowPrice: 27.2,
  bidLevels: [
    { price: 28, quantity: 45_000 },
    { price: 27.9, quantity: 31_000 },
    { price: 27.8, quantity: 16_000 },
  ],
  askLevels: [
    { price: 28.1, quantity: 21_000 },
    { price: 28.2, quantity: 24_000 },
    { price: 28.3, quantity: 33_000 },
  ],
  tradingStatus: 'Continuous',
  updatedAt: '2026-07-03T07:45:00Z',
};

describe('MarketBoard', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders loading and then the REST snapshot board', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify([quote]), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    );

    renderWithQueryClient(<MarketBoard />);

    expect(screen.getByText('Loading market board')).toBeInTheDocument();
    expect(await screen.findByRole('grid')).toBeInTheDocument();
    expect(screen.getByText('REST snapshot')).toBeInTheDocument();
    expect(screen.getByText('Bên mua')).toBeInTheDocument();
    expect(screen.getByText('Khớp lệnh')).toBeInTheDocument();
    expect(screen.getByText('Bên bán')).toBeInTheDocument();

    const row = screen.getByRole('row');
    expect(within(row).getByText('HPG')).toBeInTheDocument();
    expect(fetch).toHaveBeenCalledWith('/api/market/quotes?boardId=G1', expect.any(Object));
  });

  it('renders an error state when the market quote request fails', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response('Service unavailable', {
          status: 503,
          statusText: 'Service Unavailable',
        }),
      ),
    );

    renderWithQueryClient(<MarketBoard />);

    expect(await screen.findByText('Request failed: 503 Service Unavailable')).toBeInTheDocument();
  });
});
