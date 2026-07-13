import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MarketBoard, MarketSessionBadge, selectMarketSession } from './MarketBoard';
import { defaultMarketBoardColumnDef, marketBoardColumnDefs } from './marketBoardColumns';
import { formatChartPrice, formatCompactQuantity } from '../symbol-detail/symbolChartFormatters';
import { aggregateOhlcBarsForTimeframe, chartTimeframes } from '../symbol-detail/useSymbolDetailQueries';
import { DemoSessionControls } from '../auth/DemoSessionControls';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';
import type { QuoteHubConnectionStatus } from '../../shared/realtime/useQuoteHubConnection';
import type {
  MarketQuote,
  MarketIndexUpdate,
  MarketOhlcUpdate,
  MarketQuoteUpdate,
  MarketSessionUpdate,
  MarketTrade,
  MarketTradeUpdate,
  OhlcBar,
  QuoteStreamStatus,
  SymbolDetail,
} from '../../shared/types/market';

const testRuntime = vi.hoisted(() => ({
  applyTransactionAsync: vi.fn(),
  gridReady: false,
  realtimeOptions: undefined as
    | {
        marketBoardSubscription?: {
          boardId: string;
          symbols: string[];
        };
        onMarketIndexUpdate?: (update: MarketIndexUpdate) => void;
        onMarketSessionUpdate?: (update: MarketSessionUpdate) => void;
        onOhlcUpdate?: (update: MarketOhlcUpdate) => void;
        onQuoteUpdate: (update: MarketQuoteUpdate) => void;
        onTradeUpdate?: (update: MarketTradeUpdate) => void;
        onStreamStatus?: (status: QuoteStreamStatus) => void;
      }
    | undefined,
  realtimeState: {
    status: 'connected' as QuoteHubConnectionStatus,
    lastError: null as string | null,
  },
  setVisibleLogicalRange: vi.fn(),
  visibleLogicalRangeHandler: undefined as ((range: { from: number; to: number } | null) => void) | undefined,
}));

vi.mock('../../shared/realtime/useQuoteHubConnection', () => ({
  useQuoteHubConnection: vi.fn((options) => {
    testRuntime.realtimeOptions = options;
    return testRuntime.realtimeState;
  }),
}));

vi.mock('ag-grid-react', async () => {
  const React = await vi.importActual<typeof import('react')>('react');
  type MockColumn = {
    cellRenderer?: (params: { data: Record<string, unknown>; value: unknown }) => React.ReactNode;
    children?: MockColumn[];
    field?: string;
    headerName?: string;
    valueFormatter?: (params: { data: Record<string, unknown>; value: unknown }) => string;
  };
  const flattenColumns = (columns: MockColumn[]): MockColumn[] => columns.flatMap((column) => column.children ?? [column]);
  const renderCellValue = (column: MockColumn, row: Record<string, unknown>) => {
    const value = column.field == null ? undefined : row[column.field];
    if (column.cellRenderer) {
      return column.cellRenderer({ data: row, value });
    }

    if (column.valueFormatter) {
      return column.valueFormatter({ data: row, value });
    }

    return value == null ? '' : String(value);
  };

  return {
    AgGridReact: ({
      columnDefs,
      getRowId,
      rowData,
      onGridReady,
      onRowClicked,
    }: {
      columnDefs: MockColumn[];
      getRowId?: (params: { data: Record<string, unknown> }) => string;
      rowData: Record<string, unknown>[];
      onGridReady?: (event: { api: { applyTransactionAsync: typeof testRuntime.applyTransactionAsync } }) => void;
      onRowClicked?: (event: { data: Record<string, unknown> }) => void;
    }) => {
      const leafColumns = flattenColumns(columnDefs);

      React.useEffect(() => {
        onGridReady?.({ api: { applyTransactionAsync: testRuntime.applyTransactionAsync } });
        testRuntime.gridReady = true;
      }, [onGridReady]);

      return (
        <div role="grid">
          <div>
            {columnDefs.map((column) => [
              <span key={column.headerName}>{column.headerName}</span>,
              ...(column.children?.map((child) => <span key={`${column.headerName}-${child.headerName}`}>{child.headerName}</span>) ?? []),
            ])}
          </div>
          {rowData.map((row) => (
            <div key={getRowId?.({ data: row }) ?? String(row.symbol ?? row.id)} role="row" onClick={() => onRowClicked?.({ data: row })}>
              {leafColumns.map((column, index) => (
                <span key={`${column.field ?? column.headerName}-${index}`}>{renderCellValue(column, row)}</span>
              ))}
            </div>
          ))}
        </div>
      );
    },
  };
});

vi.mock('lightweight-charts', () => ({
  CandlestickSeries: 'CandlestickSeries',
  ColorType: {
    Solid: 'solid',
  },
  HistogramSeries: 'HistogramSeries',
  createChart: vi.fn(() => ({
    addSeries: vi.fn(() => ({
      priceScale: vi.fn(() => ({
        applyOptions: vi.fn(),
      })),
      setData: vi.fn(),
    })),
    remove: vi.fn(),
    timeScale: vi.fn(() => ({
      fitContent: vi.fn(),
      getVisibleLogicalRange: vi.fn(() => ({ from: 12, to: 24 })),
      setVisibleLogicalRange: testRuntime.setVisibleLogicalRange,
      subscribeVisibleLogicalRangeChange: vi.fn((handler: (range: { from: number; to: number } | null) => void) => {
        testRuntime.visibleLogicalRangeHandler = handler;
      }),
      unsubscribeVisibleLogicalRangeChange: vi.fn(),
    })),
  })),
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

const symbolDetail: SymbolDetail = {
  ...quote,
  finalTradeDate: null,
  isin: 'VN000000HPG4',
  listingDate: '2007-11-15T00:00:00Z',
  name: 'Hoa Phat Group Joint Stock Company',
  openInterestQuantity: 0,
  productGroupId: 'STOCK',
  securityGroupId: 'ST',
  securityType: 'Stock',
  symbolAdminStatus: 'NORMAL',
  tradingMethodStatus: 'NORMAL',
  tradingSanctionStatus: 'NORMAL',
};

const ohlcBars: OhlcBar[] = [
  {
    close: 27.8,
    high: 28,
    low: 27.4,
    open: 27.5,
    resolution: '1',
    symbol: 'HPG',
    time: '2026-07-03T07:43:00Z',
    volume: 120_000,
  },
  {
    close: 28.1,
    high: 28.2,
    low: 27.7,
    open: 27.8,
    resolution: '1',
    symbol: 'HPG',
    time: '2026-07-03T07:44:00Z',
    volume: 140_000,
  },
];

const latestTrades: MarketTrade[] = [
  {
    boardId: 'G1',
    change: 0.7,
    changePercent: 2.55,
    price: 28.1,
    quantity: 18_000,
    side: '1',
    symbol: 'HPG',
    time: '2026-07-03T07:45:00Z',
    totalValue: 347_000_000_000,
    totalVolume: 12_345_678,
  },
];

const marketSession: MarketSessionUpdate = {
  boardId: 'G1',
  eventId: 'AB2',
  isAfterHours: false,
  isAuction: false,
  isContinuous: true,
  isOpen: true,
  isPutThrough: false,
  label: 'Liên tục',
  marketId: 'HOSE',
  phase: 'CONTINUOUS',
  productGroupId: 'STO',
  source: 'REALTIME',
  tradingSessionId: '40',
  updatedAt: '2026-07-13T02:20:00.000Z',
};

const lunchBreakMarketSession: MarketSessionUpdate = {
  ...marketSession,
  eventId: '',
  isContinuous: false,
  isOpen: false,
  label: 'Nghỉ trưa',
  phase: 'LUNCH_BREAK',
  source: 'SCHEDULE_FALLBACK',
  tradingSessionId: '',
  updatedAt: '2026-07-13T05:05:00.000Z',
};

const marketIndices = [
  {
    indexName: 'VNINDEX',
    value: 1840.7,
    change: -13,
    changePercent: -0.7,
    referenceValue: 1853.7,
    highValue: 1857,
    lowValue: 1831.25,
    totalVolume: 585_707_000,
    totalValue: 14_603.675,
    upCount: 92,
    downCount: 206,
    noChangeCount: 66,
    ceilingCount: 1,
    floorCount: 3,
    marketId: 'STO',
    tradingSessionId: 'Continuous',
    updatedAt: '2026-07-03T07:45:00Z',
  },
  {
    indexName: 'VN30',
    value: 1987.11,
    change: -11.33,
    changePercent: -0.57,
    referenceValue: 1998.44,
    highValue: 2001.12,
    lowValue: 1977.64,
    totalVolume: 226_301_000,
    totalValue: 7_232.9,
    upCount: 7,
    downCount: 19,
    noChangeCount: 4,
    ceilingCount: 0,
    floorCount: 0,
    marketId: 'STO',
    tradingSessionId: 'Continuous',
    updatedAt: '2026-07-03T07:45:00Z',
  },
];

const indexOhlcBars: OhlcBar[] = [
  {
    close: 1840.7,
    high: 1857,
    low: 1831.25,
    open: 1853.7,
    resolution: '1',
    symbol: 'VNINDEX',
    time: '2026-07-03T07:45:00Z',
    volume: 585_707_000,
  },
];

describe('MarketBoard', () => {
  afterEach(() => {
    window.localStorage?.clear();
    vi.unstubAllGlobals();
    testRuntime.applyTransactionAsync.mockClear();
    testRuntime.gridReady = false;
    testRuntime.realtimeOptions = undefined;
    testRuntime.realtimeState.status = 'connected';
    testRuntime.realtimeState.lastError = null;
    testRuntime.setVisibleLogicalRange.mockClear();
    testRuntime.visibleLogicalRangeHandler = undefined;
  });

  it('renders the market session badge', () => {
    render(<MarketSessionBadge isLoading={false} session={marketSession} />);

    expect(screen.getByText(/Phiên: Liên tục/)).toBeInTheDocument();
    expect(screen.getByText(/Phiên: Liên tục/).getAttribute('title')).toContain('REALTIME');
  });

  it('prefers a newer REST session fallback over a stale realtime session', () => {
    const selectedSession = selectMarketSession(
      marketSession,
      lunchBreakMarketSession,
      { boardId: 'G1', productGroupId: 'STO' },
    );

    expect(selectedSession).toMatchObject({
      phase: 'LUNCH_BREAK',
      source: 'SCHEDULE_FALLBACK',
    });
  });

  it('renders loading and then the REST snapshot board', async () => {
    vi.stubGlobal('fetch', mockMarketBoardFetch());

    renderWithQueryClient(<MarketBoard />);

    expect(screen.getByText('Loading market board')).toBeInTheDocument();
    const userNavigation = screen.getByRole('navigation', { name: 'Chức năng người dùng' });
    const activeMarketBoardItem = within(userNavigation).getByRole('button', { name: 'Bảng giá' });
    expect(activeMarketBoardItem).toHaveAttribute('aria-current', 'page');
    expect(activeMarketBoardItem).toHaveClass('!text-xs');
    expect(within(activeMarketBoardItem).getByText('Bảng giá')).toHaveClass('!text-xs');
    expect(activeMarketBoardItem).not.toHaveClass('border-r');
    expect(within(userNavigation).queryByRole('button', { name: 'Danh mục theo dõi' })).not.toBeInTheDocument();
    expect(within(userNavigation).getByRole('button', { name: 'Đặt lệnh' })).toBeInTheDocument();
    expect(within(userNavigation).getByRole('button', { name: 'Sổ lệnh' })).toBeInTheDocument();
    expect(within(userNavigation).getByRole('button', { name: 'Danh mục nắm giữ' })).toBeInTheDocument();
    expect(within(userNavigation).getByRole('button', { name: 'Quản lý tài sản' })).toBeInTheDocument();
    expect(within(userNavigation).queryByRole('button', { name: 'Giao dịch phái sinh' })).not.toBeInTheDocument();
    expect(within(userNavigation).queryByRole('button', { name: 'Margin - Vay ký quỹ' })).not.toBeInTheDocument();
    expect(await screen.findByRole('grid')).toBeInTheDocument();
    expect((await screen.findAllByText('Liên tục')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('VNINDEX').length).toBeGreaterThan(0);
    expect(screen.getAllByText('VN30').length).toBeGreaterThan(0);
    expect(screen.getByText(/KLGD/)).toBeInTheDocument();
    expect(screen.getByText('Bên mua')).toBeInTheDocument();
    expect(screen.getByText('Khớp lệnh')).toBeInTheDocument();
    expect(screen.getByText('Bên bán')).toBeInTheDocument();
    expect(screen.getByText('ĐTNN')).toBeInTheDocument();
    expect(screen.getByText('NN mua')).toBeInTheDocument();
    expect(screen.getByText('NN bán')).toBeInTheDocument();
    expect(screen.getByText('Room')).toBeInTheDocument();
    expect(screen.getByRole('searchbox')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Danh mục của tôi' })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'VN30' }).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'HOSE' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'HNX' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'UPCOM' })).toBeInTheDocument();

    const row = screen.getByRole('row');
    expect(within(row).getByText('HPG')).toBeInTheDocument();
    await waitFor(() =>
      expect(testRuntime.realtimeOptions).toMatchObject({
        marketBoardSubscription: {
          boardId: 'G1',
          symbols: ['HPG'],
        },
      }),
    );
    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'SSI' } });
    await waitFor(() => expect(screen.queryByText('HPG')).not.toBeInTheDocument());
    expect(fetch).toHaveBeenCalledWith('/api/market/quotes?boardId=G1&indexName=VN30', expect.any(Object));
  });

  it('updates the market session badge from realtime session updates', async () => {
    vi.stubGlobal('fetch', mockMarketBoardFetch());

    renderWithQueryClient(<MarketBoard />);

    expect((await screen.findAllByText('Liên tục')).length).toBeGreaterThan(0);
    await act(async () => {
      testRuntime.realtimeOptions?.onMarketSessionUpdate?.({
        ...marketSession,
        isAuction: true,
        isContinuous: false,
        label: 'ATO',
        phase: 'ATO',
        source: 'REALTIME',
        tradingSessionId: '20',
      });
    });

    expect(screen.getAllByText('ATO').length).toBeGreaterThan(0);
  });

  it('opens and closes the trading drawer from the market toolbar', async () => {
    vi.stubGlobal('fetch', mockMarketBoardFetch());

    renderWithQueryClient(<MarketBoard />);
    expect(await screen.findByRole('grid')).toBeInTheDocument();

    const toolbar = screen.getByTestId('market-board-toolbar');
    expect(within(toolbar).queryByText('REST snapshot')).not.toBeInTheDocument();
    expect(within(toolbar).queryByText('Realtime on')).not.toBeInTheDocument();
    expect(screen.queryByTestId('market-board-status')).not.toBeInTheDocument();
    expect(screen.queryByTestId('trading-drawer')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Phieu lenh mo phong')).not.toBeInTheDocument();

    fireEvent.click(within(toolbar).getByRole('button', { name: 'Đặt lệnh' }));

    expect(await screen.findByTestId('trading-drawer')).toBeInTheDocument();
    expect(screen.getByLabelText('Phieu lenh mo phong')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Đóng bảng đặt lệnh' }));
    expect(screen.queryByTestId('trading-drawer')).not.toBeInTheDocument();
  });

  it('applies realtime quote updates to the matching grid row', async () => {
    vi.stubGlobal('fetch', mockMarketBoardFetch());

    renderWithQueryClient(<MarketBoard />);

    expect(await screen.findByRole('grid')).toBeInTheDocument();
    await waitFor(() => expect(testRuntime.gridReady).toBe(true));
    await waitFor(() =>
      expect(testRuntime.realtimeOptions?.marketBoardSubscription).toMatchObject({
        boardId: 'G1',
        symbols: ['HPG'],
      }),
    );
    const initialMarketBoardSubscription = testRuntime.realtimeOptions?.marketBoardSubscription;

    const realtimeUpdate: MarketQuoteUpdate = {
      symbol: 'HPG',
      boardId: 'G1',
      lastPrice: 28.35,
      change: 0.95,
      changePercent: 3.47,
      lastQuantity: 20_000,
      totalVolume: 12_365_678,
      totalValue: 348_000_000_000,
      foreignBuyVolume: 800_100,
      foreignSellVolume: 1_230_649,
      foreignRoom: 1_742_488_798,
      bidLevels: null,
      askLevels: null,
      tradingStatus: 'Continuous',
      updatedAt: '2026-07-03T07:45:03Z',
    };

    act(() => {
      testRuntime.realtimeOptions?.onQuoteUpdate(realtimeUpdate);
    });

    await waitFor(() => expect(screen.getByText('28.35')).toBeInTheDocument());
    expect(testRuntime.realtimeOptions?.marketBoardSubscription).toBe(initialMarketBoardSubscription);
    expect(testRuntime.applyTransactionAsync).toHaveBeenCalledWith({
      update: [expect.objectContaining({
        id: 'G1:HPG',
        lastPrice: 28.35,
        flashClasses: expect.objectContaining({ lastPrice: 'up', lastQuantity: 'up' }),
      })],
    });
  });

  it('keeps the REST snapshot usable when realtime is offline', async () => {
    testRuntime.realtimeState.status = 'error';
    testRuntime.realtimeState.lastError = 'Connection failed';
    vi.stubGlobal('fetch', mockMarketBoardFetch());

    renderWithQueryClient(<MarketBoard />);

    expect(await screen.findByRole('grid')).toBeInTheDocument();
    expect(screen.queryByTestId('market-board-status')).not.toBeInTheDocument();
    expect(screen.getByText('HPG')).toBeInTheDocument();
  });

  it('requests market quotes again when the active market filter changes', async () => {
    vi.stubGlobal('fetch', mockMarketBoardFetch());

    renderWithQueryClient(<MarketBoard />);

    expect(await screen.findByRole('grid')).toBeInTheDocument();
    expect(fetch).toHaveBeenCalledWith('/api/market/quotes?boardId=G1&indexName=VN30', expect.any(Object));

    fireEvent.click(screen.getByRole('button', { name: 'HNX' }));

    await waitFor(() =>
      expect(fetch).toHaveBeenCalledWith('/api/market/quotes?boardId=G1&marketId=STX', expect.any(Object)),
    );
  });

  it('requests market quotes by symbols when a watchlist group is selected', async () => {
    const fetchMock = mockMarketBoardFetch([quote], [{
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
    }]);
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<><DemoSessionControls /><MarketBoard /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith('/api/watchlist', expect.any(Object)),
    );
    expect(await screen.findByRole('grid')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Danh mục của tôi' }));
    fireEvent.click(await screen.findByRole('button', { name: /TK H197731/ }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith('/api/market/quotes?boardId=G1&symbols=HPG', expect.any(Object)),
    );
  });

  it('adds the open symbol detail symbol to a selected watchlist group without changing the board filter', async () => {
    const fetchMock = mockMarketBoardFetch([quote], [{
      id: 'group-1',
      name: 'TK H197731',
      createdAt: '2026-07-12T09:00:00Z',
      updatedAt: '2026-07-12T09:00:00Z',
      items: [],
    }]);
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<><DemoSessionControls /><MarketBoard /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith('/api/watchlist', expect.any(Object)),
    );

    const row = await screen.findByRole('row');
    fireEvent.click(row);
    const panel = await screen.findByTestId('symbol-detail-panel');
    fireEvent.click(within(panel).getByRole('button', { name: 'Theo dõi HPG' }));
    fireEvent.click(await within(panel).findByRole('button', { name: /TK H197731/ }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/watchlist/group-1/items',
        expect.objectContaining({
          body: JSON.stringify({ boardId: 'G1', symbol: 'HPG' }),
          method: 'POST',
        }),
      ),
    );
    expect(fetchMock).not.toHaveBeenCalledWith('/api/market/quotes?boardId=G1&symbols=HPG', expect.any(Object));
  });

  it('closes the symbol detail watchlist picker when clicking outside', async () => {
    const fetchMock = mockMarketBoardFetch([quote], [{
      id: 'group-1',
      name: 'TK H197731',
      createdAt: '2026-07-12T09:00:00Z',
      updatedAt: '2026-07-12T09:00:00Z',
      items: [],
    }]);
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<><DemoSessionControls /><MarketBoard /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    const row = await screen.findByRole('row');
    fireEvent.click(row);
    const panel = await screen.findByTestId('symbol-detail-panel');
    fireEvent.click(within(panel).getByRole('button', { name: 'Theo dõi HPG' }));

    expect(await within(panel).findByRole('dialog', { name: 'Chọn danh mục cho HPG' })).toBeInTheDocument();

    fireEvent.pointerDown(screen.getByTestId('market-board-toolbar'));

    await waitFor(() =>
      expect(within(panel).queryByRole('dialog', { name: 'Chọn danh mục cho HPG' })).not.toBeInTheDocument(),
    );
  });

  it('applies realtime market index updates to the index overview', async () => {
    vi.stubGlobal('fetch', mockMarketBoardFetch());

    renderWithQueryClient(<MarketBoard />);

    expect(await screen.findByRole('grid')).toBeInTheDocument();
    await waitFor(() => expect(screen.getAllByText('VNINDEX').length).toBeGreaterThan(0));

    act(() => {
      testRuntime.realtimeOptions?.onMarketIndexUpdate?.({
        indexName: 'VNINDEX',
        value: 1850,
        change: 10,
        changePercent: 0.54,
        referenceValue: 1840,
        highValue: 1851,
        lowValue: 1830,
        totalVolume: 600_000_000,
        totalValue: 15_000,
        upCount: 100,
        downCount: 150,
        noChangeCount: 70,
        ceilingCount: 2,
        floorCount: 1,
        marketId: 'STO',
        tradingSessionId: 'Continuous',
        updatedAt: '2026-07-03T07:46:00Z',
      });
    });

    await waitFor(() => expect(screen.getByText('↑1,850.00 (+10.00 +0.54%)')).toBeInTheDocument());
  });

  it('opens the symbol detail panel from a market board row', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => {
      const url = input.toString();
      if (url.startsWith('/api/market/symbols/HPG/trades/latest')) {
        return Promise.resolve(jsonResponse(latestTrades));
      }

      if (url.startsWith('/api/market/symbols/HPG/ohlc')) {
        return Promise.resolve(jsonResponse(ohlcBars));
      }

      if (url.startsWith('/api/market/symbols/HPG?')) {
        return Promise.resolve(jsonResponse(symbolDetail));
      }

      if (url.startsWith('/api/market/session')) {
        return Promise.resolve(jsonResponse(marketSession));
      }

      return Promise.resolve(jsonResponse([quote]));
    }));

    renderWithQueryClient(<MarketBoard />);

    const row = await screen.findByRole('row');
    fireEvent.click(row);

    expect(await screen.findByTestId('symbol-detail-panel')).toBeInTheDocument();
    expect(await screen.findByText('Giao dịch')).toBeInTheDocument();
    expect(await screen.findByText('Độ sâu thị trường')).toBeInTheDocument();
    await waitFor(() => expect(screen.getAllByText('Khớp lệnh').length).toBeGreaterThan(1));
    expect((await screen.findAllByText('Đặt lệnh')).length).toBeGreaterThan(0);
    expect(await screen.findByTestId('symbol-price-chart')).toBeInTheDocument();
    const panel = await screen.findByTestId('symbol-detail-panel');
    await waitFor(() => expect(within(panel).getByText('14:45:00')).toBeInTheDocument());
    expect(within(panel).getByText('18,000')).toBeInTheDocument();
    expect(within(panel).getAllByText('+0.70').length).toBeGreaterThan(0);
    expect(within(panel).getAllByText('+2.55%').length).toBeGreaterThan(0);
    expect(fetch).toHaveBeenCalledWith('/api/market/symbols/HPG?boardId=G1', expect.any(Object));
    expect(fetch).toHaveBeenCalledWith(expect.stringMatching(/^\/api\/market\/symbols\/HPG\/ohlc\?resolution=1D&from=/), expect.any(Object));
    expect(fetch).toHaveBeenCalledWith('/api/market/symbols/HPG/trades/latest?boardId=G1&limit=30', expect.any(Object));

    fireEvent.click(screen.getByRole('button', { name: '30m' }));

    await waitFor(() =>
      expect(fetch).toHaveBeenCalledWith(expect.stringMatching(/^\/api\/market\/symbols\/HPG\/ohlc\?resolution=30&from=/), expect.any(Object)),
    );

    fireEvent.click(screen.getByRole('button', { name: '4H' }));

    await waitFor(() =>
      expect(fetch).toHaveBeenCalledWith(expect.stringMatching(/^\/api\/market\/symbols\/HPG\/ohlc\?resolution=1H&from=/), expect.any(Object)),
    );
    expect(fetch).not.toHaveBeenCalledWith(expect.stringMatching(/^\/api\/market\/symbols\/HPG\/ohlc\?resolution=4H&from=/), expect.any(Object));
  });

  it('updates the symbol detail panel from realtime quote updates', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => {
      const url = input.toString();
      if (url.startsWith('/api/market/symbols/HPG/trades/latest')) {
        return Promise.resolve(jsonResponse(latestTrades));
      }

      if (url.startsWith('/api/market/symbols/HPG/ohlc')) {
        return Promise.resolve(jsonResponse(ohlcBars));
      }

      if (url.startsWith('/api/market/symbols/HPG?')) {
        return Promise.resolve(jsonResponse(symbolDetail));
      }

      if (url.startsWith('/api/market/session')) {
        return Promise.resolve(jsonResponse(marketSession));
      }

      return Promise.resolve(jsonResponse([quote]));
    }));

    renderWithQueryClient(<MarketBoard />);

    const row = await screen.findByRole('row');
    fireEvent.click(row);
    const panel = await screen.findByTestId('symbol-detail-panel');
    await waitFor(() => expect(within(panel).getAllByText('28.10').length).toBeGreaterThan(0));

    const realtimeUpdate: MarketQuoteUpdate = {
      symbol: 'HPG',
      boardId: 'G1',
      lastPrice: 28.35,
      change: 0.95,
      changePercent: 3.47,
      lastQuantity: 20_000,
      totalVolume: 12_365_678,
      totalValue: 348_000_000_000,
      foreignBuyVolume: 800_100,
      foreignSellVolume: 1_230_649,
      foreignRoom: 1_742_488_798,
      bidLevels: [{ price: 28.2, quantity: 55_000 }],
      askLevels: [{ price: 28.4, quantity: 29_000 }],
      tradingStatus: 'Continuous',
      updatedAt: '2026-07-03T07:45:03Z',
    };

    act(() => {
      testRuntime.realtimeOptions?.onQuoteUpdate(realtimeUpdate);
    });

    await waitFor(() => expect(within(panel).getAllByText('28.35').length).toBeGreaterThan(0));
    expect(within(panel).getAllByText('55,000').length).toBeGreaterThan(0);
    expect(within(panel).getAllByText('28.20').length).toBeGreaterThan(0);
    expect(within(panel).getByText('C28.35')).toBeInTheDocument();

    const realtimeTradeUpdate: MarketTradeUpdate = {
      symbol: 'HPG',
      boardId: 'G1',
      time: '2026-07-03T07:45:04Z',
      price: 28_350,
      change: null,
      changePercent: null,
      quantity: 20_000,
      totalVolume: 12_365_678,
      totalValue: 348_000_000_000,
      side: 'S',
    };

    act(() => {
      testRuntime.realtimeOptions?.onTradeUpdate?.(realtimeTradeUpdate);
    });

    await waitFor(() => expect(within(panel).getAllByText('20,000').length).toBeGreaterThan(0));
    expect(within(panel).getByText('S')).toBeInTheDocument();
    expect(within(panel).getAllByText('+0.95').length).toBeGreaterThan(0);
    expect(within(panel).getAllByText('+3.47%').length).toBeGreaterThan(0);
  });

  it('loads older OHLC bars when the chart is panned near the oldest loaded bar', async () => {
    const olderBars: OhlcBar[] = [
      {
        close: 26.9,
        high: 27.1,
        low: 26.5,
        open: 26.7,
        resolution: '1',
        symbol: 'HPG',
        time: '2026-07-02T07:44:00Z',
        volume: 90_000,
      },
    ];
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = input.toString();
      if (url.startsWith('/api/market/symbols/HPG/trades/latest')) {
        return Promise.resolve(jsonResponse(latestTrades));
      }

      if (url.startsWith('/api/market/symbols/HPG/ohlc')) {
        const requestUrl = new URL(url, 'http://localhost');
        const requestedTo = requestUrl.searchParams.get('to');
        const oldestInitialBarTime = new Date(ohlcBars[0].time).getTime();
        if (requestedTo != null && new Date(requestedTo).getTime() < oldestInitialBarTime) {
          return Promise.resolve(jsonResponse(olderBars));
        }

        return Promise.resolve(jsonResponse(ohlcBars));
      }

      if (url.startsWith('/api/market/symbols/HPG?')) {
        return Promise.resolve(jsonResponse(symbolDetail));
      }

      if (url.startsWith('/api/market/session')) {
        return Promise.resolve(jsonResponse(marketSession));
      }

      return Promise.resolve(jsonResponse([quote]));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<MarketBoard />);

    const row = await screen.findByRole('row');
    fireEvent.click(row);
    expect(await screen.findByTestId('symbol-price-chart')).toBeInTheDocument();

    await waitFor(() => expect(testRuntime.visibleLogicalRangeHandler).toBeDefined());
    act(() => {
      testRuntime.visibleLogicalRangeHandler?.({ from: 8, to: 24 });
    });

    await waitFor(() => {
      const ohlcCalls = fetchMock.mock.calls
        .map(([input]) => input.toString())
        .filter((url) => url.startsWith('/api/market/symbols/HPG/ohlc'));
      expect(ohlcCalls.length).toBeGreaterThan(1);
      const previousPageUrl = new URL(ohlcCalls[1], 'http://localhost');
      expect(new Date(previousPageUrl.searchParams.get('to') ?? '').getTime()).toBeLessThan(new Date(ohlcBars[0].time).getTime());
    });
  });

  it('aggregates hourly OHLC bars for the 4H chart timeframe', () => {
    const fourHourTimeframe = chartTimeframes.find((timeframe) => timeframe.id === '4h');

    expect(fourHourTimeframe).toBeDefined();
    const result = aggregateOhlcBarsForTimeframe([
      {
        close: 10.5,
        high: 11,
        low: 9.8,
        open: 10,
        resolution: '1H',
        symbol: 'SSI',
        time: '2026-07-08T00:00:00Z',
        volume: 100,
      },
      {
        close: 11.5,
        high: 12,
        low: 10.3,
        open: 10.5,
        resolution: '1H',
        symbol: 'SSI',
        time: '2026-07-08T01:00:00Z',
        volume: 200,
      },
      {
        close: 12.5,
        high: 13,
        low: 11.2,
        open: 11.5,
        resolution: '1H',
        symbol: 'SSI',
        time: '2026-07-08T04:00:00Z',
        volume: 300,
      },
    ], fourHourTimeframe!);

    expect(result).toHaveLength(2);
    expect(result[0]).toMatchObject({
      close: 11.5,
      high: 12,
      low: 9.8,
      open: 10,
      resolution: '4H',
      volume: 300,
    });
    expect(result[1]).toMatchObject({
      close: 12.5,
      high: 13,
      low: 11.2,
      open: 11.5,
      resolution: '4H',
      volume: 300,
    });
  });

  it('formats symbol chart prices and volumes like the trading board', () => {
    expect(formatChartPrice(35_000)).toBe('35.00');
    expect(formatChartPrice(35.5)).toBe('35.50');
    expect(formatCompactQuantity(3_345_000)).toBe('3.345M');
    expect(formatCompactQuantity(800_123)).toBe('800.123K');
    expect(formatCompactQuantity(900)).toBe('900');
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
      vi.fn(() =>
        Promise.resolve(new Response('Service unavailable', {
          status: 503,
          statusText: 'Service Unavailable',
        })),
      ),
    );

    renderWithQueryClient(<MarketBoard />);

    expect(await screen.findByText('Request failed: 503 Service Unavailable')).toBeInTheDocument();
  });
});

function jsonResponse(body: unknown, init?: ResponseInit) {
  return new Response(JSON.stringify(body), {
    status: init?.status ?? 200,
    statusText: init?.statusText,
    headers: { 'Content-Type': 'application/json' },
  });
}

function mockMarketBoardFetch(
  quotes: MarketQuote[] = [quote],
  watchlistGroups: unknown[] = [],
) {
  let groups = watchlistGroups as Array<{
    id: string;
    name: string;
    createdAt: string;
    updatedAt: string;
    items: Array<{ id: string; groupId: string; symbol: string; boardId: string; createdAt: string }>;
  }>;

  return vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = input.toString();
    const method = init?.method ?? 'GET';

    if (url.startsWith('/api/auth/demo-login')) {
      return Promise.resolve(jsonResponse({
        accessToken: 'test-token',
        tokenType: 'Bearer',
        expiresAt: '2099-01-01T00:00:00Z',
        user: {
          id: '6b4e73e5-53f6-421c-bcec-24c2dfbcd4a5',
          email: 'demo@investview.local',
          displayName: 'InvestView Demo',
        },
      }));
    }

    if (url.startsWith('/api/me')) {
      return Promise.resolve(jsonResponse({
        id: '6b4e73e5-53f6-421c-bcec-24c2dfbcd4a5',
        email: 'demo@investview.local',
        displayName: 'InvestView Demo',
        cashAccounts: [],
      }));
    }

    if (url === '/api/watchlist' && method === 'GET') {
      return Promise.resolve(jsonResponse(groups));
    }

    const addWatchlistItemMatch = url.match(/^\/api\/watchlist\/([^/]+)\/items$/);
    if (addWatchlistItemMatch && method === 'POST') {
      const groupId = decodeURIComponent(addWatchlistItemMatch[1]);
      const body = JSON.parse(String(init?.body));
      const item = {
        id: 'item-1',
        groupId,
        symbol: String(body.symbol).trim().toUpperCase(),
        boardId: String(body.boardId).trim().toUpperCase(),
        createdAt: '2026-07-12T09:01:00Z',
      };
      groups = groups.map((group) =>
        group.id === groupId
          ? { ...group, items: [...group.items.filter((existing) => existing.symbol !== item.symbol || existing.boardId !== item.boardId), item] }
          : group,
      );
      return Promise.resolve(jsonResponse(item, { status: 201 }));
    }

    const deleteWatchlistItemMatch = url.match(/^\/api\/watchlist\/([^/]+)\/items\/([^/]+)\/([^/]+)$/);
    if (deleteWatchlistItemMatch && method === 'DELETE') {
      const groupId = decodeURIComponent(deleteWatchlistItemMatch[1]);
      const boardId = decodeURIComponent(deleteWatchlistItemMatch[2]);
      const symbol = decodeURIComponent(deleteWatchlistItemMatch[3]);
      groups = groups.map((group) =>
        group.id === groupId
          ? { ...group, items: group.items.filter((item) => item.boardId !== boardId || item.symbol !== symbol) }
          : group,
      );
      return Promise.resolve(new Response(null, { status: 204 }));
    }

    if (url.startsWith('/api/market/symbols/HPG/trades/latest')) {
      return Promise.resolve(jsonResponse(latestTrades));
    }

    if (url.startsWith('/api/market/symbols/HPG/ohlc')) {
      return Promise.resolve(jsonResponse(ohlcBars));
    }

    if (url.startsWith('/api/market/symbols/HPG?')) {
      return Promise.resolve(jsonResponse(symbolDetail));
    }

    if (url.startsWith('/api/market/session')) {
      return Promise.resolve(jsonResponse(marketSession));
    }

    if (url.startsWith('/api/market/indices/') && url.includes('/ohlc')) {
      return Promise.resolve(jsonResponse(indexOhlcBars));
    }

    if (url.startsWith('/api/market/indices')) {
      return Promise.resolve(jsonResponse(marketIndices));
    }

    return Promise.resolve(jsonResponse(quotes));
  });
}
