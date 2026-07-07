import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MarketBoard } from './MarketBoard';
import { defaultMarketBoardColumnDef, marketBoardColumnDefs } from './marketBoardColumns';
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
        {columnDefs.map((column) => [
          <span key={column.headerName}>{column.headerName}</span>,
          ...(column.children?.map((child) => <span key={`${column.headerName}-${child.headerName}`}>{child.headerName}</span>) ?? []),
        ])}
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
  foreignBuyVolume: 786_100,
  foreignSellVolume: 1_227_649,
  foreignRoom: 1_742_502_798,
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
    expect(screen.getByText('ĐTNN')).toBeInTheDocument();
    expect(screen.getByText('NN mua')).toBeInTheDocument();
    expect(screen.getByText('NN bán')).toBeInTheDocument();
    expect(screen.getByText('Room')).toBeInTheDocument();
    expect(screen.getByRole('searchbox')).toBeInTheDocument();
    expect(screen.getByLabelText('User market list')).toBeInTheDocument();

    const indexMarketList = screen.getByLabelText('Index market list') as HTMLSelectElement;
    expect(indexMarketList.value).toBe('VN30');
    expect(screen.queryByRole('button', { name: 'VN30' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'HOSE' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'HNX' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'UPCOM' })).toBeInTheDocument();

    const row = screen.getByRole('row');
    expect(within(row).getByText('HPG')).toBeInTheDocument();
    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'SSI' } });
    await waitFor(() => expect(screen.queryByText('HPG')).not.toBeInTheDocument());
    expect(fetch).toHaveBeenCalledWith('/api/market/quotes?boardId=G1&indexName=VN30', expect.any(Object));
  });

  it('requests market quotes again when the active market filter changes', async () => {
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

    expect(await screen.findByRole('grid')).toBeInTheDocument();
    expect(fetch).toHaveBeenCalledWith('/api/market/quotes?boardId=G1&indexName=VN30', expect.any(Object));

    fireEvent.click(screen.getByRole('button', { name: 'HNX' }));

    await waitFor(() =>
      expect(fetch).toHaveBeenCalledWith('/api/market/quotes?boardId=G1&marketId=STX', expect.any(Object)),
    );

    fireEvent.change(screen.getByLabelText('Index market list'), { target: { value: 'VN100' } });

    await waitFor(() =>
      expect(fetch).toHaveBeenCalledWith('/api/market/quotes?boardId=G1&indexName=VN100', expect.any(Object)),
    );
  });

  it('keeps the symbol pin column before the stock code column', () => {
    expect(marketBoardColumnDefs[0]).toMatchObject({
      colId: 'pinSymbol',
      pinned: 'left',
      lockPinned: true,
    });
    expect(marketBoardColumnDefs[1]).toMatchObject({
      field: 'symbol',
      pinned: 'left',
      lockPinned: true,
    });
  });

  it('enables sorting for market data columns and sorts stock codes alphabetically', () => {
    const symbolColumn = marketBoardColumnDefs[1];

    expect(defaultMarketBoardColumnDef.sortable).toBe(true);
    expect(defaultMarketBoardColumnDef.unSortIcon).toBeUndefined();
    expect(marketBoardColumnDefs[0]).toMatchObject({ sortable: false });
    expect(symbolColumn).toMatchObject({ field: 'symbol', sortable: true });
    expect('comparator' in symbolColumn).toBe(true);

    const comparator = 'comparator' in symbolColumn ? symbolColumn.comparator : undefined;
    expect(typeof comparator).toBe('function');
    if (typeof comparator === 'function') {
      expect(comparator('AAA', 'BBB', undefined as never, undefined as never, false)).toBeLessThan(0);
      expect(comparator('SSI', 'HPG', undefined as never, undefined as never, false)).toBeGreaterThan(0);
    }
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
