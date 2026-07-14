import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { OrderTicketPanel } from './OrderTicketPanel';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';
import type { MarketQuote, MarketSessionUpdate } from '../../shared/types/market';
import { DemoSessionControls } from '../auth/DemoSessionControls';

describe('OrderTicketPanel', () => {
  afterEach(() => {
    window.localStorage?.clear();
    vi.unstubAllGlobals();
  });

  it('logs in and submits a simulated MTL order for the selected symbol', async () => {
    const fetchMock = vi.fn(createOrderTicketFetch());
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<><DemoSessionControls /><OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    await screen.findByRole('button', { name: 'Mua HPG' });
    fireEvent.change(screen.getByLabelText('Khối lượng'), { target: { value: '100' } });
    fireEvent.click(screen.getByRole('button', { name: 'Mua HPG' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/orders',
        expect.objectContaining({
          body: JSON.stringify({
            symbol: 'HPG',
            boardId: 'G1',
            side: 'Buy',
            orderType: 'MTL',
            quantity: 100,
            limitPrice: null,
          }),
          headers: expect.objectContaining({ Authorization: 'Bearer test-token' }),
          method: 'POST',
        }),
      ),
    );
    expect(await screen.findByRole('row', { name: 'HPG Mua MTL Đã khớp' })).toBeInTheDocument();
  });

  it('enables LO price entry and submits a simulated LO order', async () => {
    const fetchMock = vi.fn(createOrderTicketFetch({
      orderType: 'LO',
      limitPrice: 28_000,
      status: 'New',
      filledQuantity: 0,
      averageFillPrice: null,
      executions: [],
    }));
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<><DemoSessionControls /><OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    await screen.findByRole('button', { name: 'LO' });
    fireEvent.click(screen.getByRole('button', { name: 'LO' }));
    fireEvent.change(screen.getByLabelText('Khối lượng'), { target: { value: '100' } });
    fireEvent.change(screen.getByLabelText('Giá LO (x1000 VNĐ)'), { target: { value: '28' } });
    fireEvent.click(screen.getByRole('button', { name: 'Mua HPG' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/orders',
        expect.objectContaining({
          body: JSON.stringify({
            symbol: 'HPG',
            boardId: 'G1',
            side: 'Buy',
            orderType: 'LO',
            quantity: 100,
            limitPrice: 28_000,
          }),
          method: 'POST',
        }),
      ),
    );
    expect(await screen.findByRole('row', { name: 'HPG Mua LO Chờ khớp' })).toBeInTheDocument();
  });

  it('prefills pending orders for edit and cancels pending orders from the ledger', async () => {
    const pendingOrder = createSimulatedOrder({
      id: '99f8855f-d560-430f-8335-49f81f58a74f',
      orderType: 'LO',
      limitPrice: 28_000,
      status: 'New',
      filledQuantity: 0,
      averageFillPrice: null,
      executions: [],
    });
    const fetchMock = vi.fn(createOrderTicketFetch({ orders: [pendingOrder] }));
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<><DemoSessionControls /><OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    const pendingRow = await screen.findByRole('row', { name: 'HPG Mua LO Chờ khớp' });

    fireEvent.click(within(pendingRow).getByRole('button', { name: 'Sửa' }));
    expect(screen.getByRole('button', { name: 'LO' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByLabelText('Khối lượng')).toHaveValue('100');
    expect(screen.getByLabelText('Giá LO (x1000 VNĐ)')).toHaveValue('28');

    fireEvent.click(within(pendingRow).getByRole('button', { name: 'Hủy' }));
    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/orders/99f8855f-d560-430f-8335-49f81f58a74f/cancel',
        expect.objectContaining({
          headers: expect.objectContaining({ Authorization: 'Bearer test-token' }),
          method: 'POST',
        }),
      ),
    );
  });

  it('disables submit when quantity is invalid', async () => {
    vi.stubGlobal('fetch', vi.fn(createOrderTicketFetch()));

    renderWithQueryClient(<><DemoSessionControls /><OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    await screen.findByRole('button', { name: 'Mua HPG' });
    fireEvent.change(screen.getByLabelText('Khối lượng'), { target: { value: '0' } });

    expect(screen.getByText('Khối lượng phải là số nguyên dương.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Mua HPG' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Bán HPG' })).toBeDisabled();
  });

  it('renders the resolved market session label instead of the raw trading status code', () => {
    renderWithQueryClient(
      <OrderTicketPanel
        liveQuote={{ ...quote, tradingStatus: '40' }}
        marketSession={continuousMarketSession}
        selection={{ symbol: 'HPG', boardId: 'G1' }}
      />,
    );

    expect(screen.getByText('Liên tục')).toBeInTheDocument();
    expect(screen.queryByText('40')).not.toBeInTheDocument();
  });

  it('blocks simulated order entry when the market session is closed', async () => {
    const fetchMock = vi.fn(createOrderTicketFetch());
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(
      <>
        <DemoSessionControls />
        <OrderTicketPanel
          liveQuote={quote}
          marketSession={closedMarketSession}
          selection={{ symbol: 'HPG', boardId: 'G1' }}
        />
      </>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    expect(await screen.findByText('Ngoài giờ giao dịch, không thể đặt lệnh mô phỏng.')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Khối lượng'), { target: { value: '100' } });
    expect(screen.getByRole('button', { name: 'Mua HPG' })).toBeDisabled();

    expect(fetchMock.mock.calls.some(([url, init]) =>
      url.toString() === '/api/orders' && (init as RequestInit | undefined)?.method === 'POST',
    )).toBe(false);
  });

  it('shows a business message when the API rejects an order outside market hours', async () => {
    vi.stubGlobal('fetch', vi.fn(createOrderTicketFetch({
      orderProblemTitle: 'Market is not open for simulated orders.',
    })));

    renderWithQueryClient(<><DemoSessionControls /><OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    await screen.findByRole('button', { name: 'Mua HPG' });
    fireEvent.change(screen.getByLabelText('Khối lượng'), { target: { value: '100' } });
    fireEvent.click(screen.getByRole('button', { name: 'Mua HPG' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Ngoài giờ giao dịch, không thể đặt lệnh mô phỏng.');
  });

  it('renders the simplified simulated trading ticket and keeps entry gated behind login', () => {
    renderWithQueryClient(<OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} />);

    expect(screen.getByText('HPG')).toBeInTheDocument();
    expect(screen.getByText('(HOSE)')).toBeInTheDocument();
    expect(screen.getByText('Tài khoản')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Tài khoản đặt lệnh' })).toHaveAttribute('data-slot', 'select-trigger');
    expect(screen.getByText('Sức mua')).toBeInTheDocument();
    expect(screen.getByText('Đang nắm giữ')).toBeInTheDocument();
    expect(screen.getByText('Có thể bán')).toBeInTheDocument();
    expect(screen.getByText('Loại lệnh')).toBeInTheDocument();
    expect(screen.getByLabelText('Khối lượng')).toHaveAttribute('data-slot', 'input-group-control');
    expect(screen.getByLabelText('Giá LO (x1000 VNĐ)')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'MTL' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'LO' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ATO' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'ATC' })).not.toBeInTheDocument();
    expect(screen.queryByText('Kiểu xác thực')).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Sổ lệnh' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Sổ lệnh điều kiện' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Danh mục' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Tài sản' })).toBeInTheDocument();
    expect(screen.getByText('Đăng nhập ở góc trên bên phải để đặt lệnh mô phỏng.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Mua HPG' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Mua HPG' })).toHaveAttribute('data-slot', 'button');
    expect(screen.getByRole('button', { name: 'Bán HPG' })).toBeDisabled();

    const conditionalOrdersTab = screen.getByRole('tab', { name: 'Sổ lệnh điều kiện' });
    fireEvent.mouseDown(conditionalOrdersTab, { button: 0, ctrlKey: false });
    fireEvent.click(conditionalOrdersTab);
    expect(screen.getByText('Chưa có lệnh điều kiện')).toBeInTheDocument();
  });

  it('renders portfolio assets as compact Vietnamese summary rows', async () => {
    vi.stubGlobal('fetch', vi.fn(createOrderTicketFetch()));

    renderWithQueryClient(<><DemoSessionControls /><OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    const assetsTab = await screen.findByRole('tab', { name: 'Tài sản' });
    fireEvent.mouseDown(assetsTab, { button: 0, ctrlKey: false });
    fireEvent.click(assetsTab);

    const assetsRegion = await screen.findByRole('region', { name: 'Tài sản tài khoản mô phỏng' });
    expect(within(assetsRegion).getByRole('table', { name: 'Tổng hợp tài sản' })).toBeInTheDocument();
    expect(within(assetsRegion).getByText('Tổng tài sản TKCK')).toBeInTheDocument();
    expect(within(assetsRegion).getByText('Sức mua tối đa')).toBeInTheDocument();
    expect(within(assetsRegion).getByText('Tổng tài sản thực có')).toBeInTheDocument();
    expect(within(assetsRegion).getByText('Số dư tiền')).toBeInTheDocument();
    expect(within(assetsRegion).getByText('Giá trị CK niêm yết')).toBeInTheDocument();
    expect(within(assetsRegion).getByText('Tiền có thể rút')).toBeInTheDocument();
    expect(within(assetsRegion).getAllByText('100.000.000')).toHaveLength(2);
    expect(within(assetsRegion).getAllByText('97.085.000')).toHaveLength(3);
    expect(within(assetsRegion).getByText('2.915.000')).toBeInTheDocument();
  });

  it('renders holdings as a Vietnamese compact table in the portfolio tab', async () => {
    vi.stubGlobal('fetch', vi.fn(createOrderTicketFetch()));

    renderWithQueryClient(<><DemoSessionControls /><OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    const holdingsTab = await screen.findByRole('tab', { name: 'Danh mục' });
    fireEvent.mouseDown(holdingsTab, { button: 0, ctrlKey: false });
    fireEvent.click(holdingsTab);

    const holdingsTable = await screen.findByRole('table', { name: 'Danh mục chứng khoán nắm giữ' });
    expect(within(holdingsTable).getByRole('columnheader', { name: 'Mã CK' })).toBeInTheDocument();
    expect(within(holdingsTable).getByRole('columnheader', { name: /KLGD\s*Tổng KL/ })).toBeInTheDocument();
    expect(within(holdingsTable).getByRole('columnheader', { name: /Giá TT\s*Giá vốn/ })).toBeInTheDocument();
    expect(within(holdingsTable).getByRole('columnheader', { name: 'Giá trị TT' })).toBeInTheDocument();
    expect(within(holdingsTable).getByRole('columnheader', { name: /Lãi\/Lỗ\s*Lãi\/Lỗ \(%\)/ })).toBeInTheDocument();
    expect(within(holdingsTable).getByRole('columnheader', { name: 'Bán' })).toBeInTheDocument();
    expect(within(holdingsTable).getByText('HPG')).toBeInTheDocument();
    expect(within(holdingsTable).getByText('Có thể bán 100')).toBeInTheDocument();
    expect(within(holdingsTable).getByText('2,915,000 VND')).toBeInTheDocument();
  });
});

const quote: MarketQuote = {
  symbol: 'HPG',
  boardId: 'G1',
  marketId: 'HOSE',
  displayName: 'Hoa Phat Group',
  referencePrice: 28_600,
  ceilingPrice: 30_600,
  floorPrice: 26_600,
  lastPrice: 29_150,
  change: 550,
  changePercent: 1.92,
  lastQuantity: 2_500,
  totalVolume: 12_450_000,
  totalValue: 362_917_500_000,
  foreignBuyVolume: 786_100,
  foreignSellVolume: 1_227_649,
  foreignRoom: 1_742_502_798,
  openPrice: 28_700,
  highPrice: 29_200,
  lowPrice: 28_450,
  bidLevels: [],
  askLevels: [],
  tradingStatus: 'Continuous',
  updatedAt: '2026-07-03T07:45:00Z',
};

const continuousMarketSession: MarketSessionUpdate = {
  marketId: 'HOSE',
  boardId: 'G1',
  productGroupId: 'STO',
  eventId: 'AB2',
  tradingSessionId: '40',
  updatedAt: '2026-07-03T07:45:00Z',
  phase: 'CONTINUOUS',
  label: 'Liên tục',
  isOpen: true,
  isAuction: false,
  isContinuous: true,
  isPutThrough: false,
  isAfterHours: false,
  source: 'REALTIME',
};

const closedMarketSession: MarketSessionUpdate = {
  ...continuousMarketSession,
  eventId: 'CLOSED',
  tradingSessionId: '99',
  phase: 'CLOSED',
  label: 'Đã đóng cửa',
  isOpen: false,
  isContinuous: false,
};

type OrderResponseOverride = {
  id?: string;
  orderProblemTitle?: string;
  orderType?: 'LO' | 'MTL';
  limitPrice?: number | null;
  status?: 'New' | 'Filled';
  filledQuantity?: number;
  averageFillPrice?: number | null;
  executions?: unknown[];
  orders?: unknown[];
};

function createOrderTicketFetch(override: OrderResponseOverride = {}) {
  return (input: RequestInfo | URL, init?: RequestInit) => {
    const url = input.toString();
    const method = init?.method ?? 'GET';

    if (url === '/api/auth/demo-login' && method === 'POST') {
      return Promise.resolve(jsonResponse(createDemoSession()));
    }

    if (url === '/api/orders' && method === 'POST') {
      if (override.orderProblemTitle != null) {
        return Promise.resolve(jsonResponse({
          title: override.orderProblemTitle,
        }, {
          status: 400,
          statusText: 'Bad Request',
          headers: { 'Content-Type': 'application/problem+json' },
        }));
      }

      const orderType = override.orderType ?? 'MTL';
      const status = override.status ?? 'Filled';
      const filledQuantity = override.filledQuantity ?? 100;
      return Promise.resolve(jsonResponse(createSimulatedOrder({
        id: override.id,
        orderType,
        limitPrice: override.limitPrice ?? null,
        status,
        filledQuantity,
        averageFillPrice: override.averageFillPrice === undefined ? 29_150 : override.averageFillPrice,
        executions: override.executions,
      }), { status: 201 }));
    }

    if (url === '/api/orders' && method === 'GET') {
      return Promise.resolve(jsonResponse(override.orders ?? []));
    }

    const cancelOrderMatch = url.match(/^\/api\/orders\/([^/]+)\/cancel$/);
    if (cancelOrderMatch && method === 'POST') {
      const orderId = decodeURIComponent(cancelOrderMatch[1]);
      const order = (override.orders ?? []).find((candidate) =>
        typeof candidate === 'object' &&
        candidate != null &&
        'id' in candidate &&
        candidate.id === orderId,
      );
      return Promise.resolve(jsonResponse({
        ...(order ?? createSimulatedOrder({ id: orderId, status: 'New', filledQuantity: 0, averageFillPrice: null, executions: [] })),
        status: 'Cancelled',
        updatedAt: '2026-07-12T09:05:00Z',
      }));
    }

    if (url === '/api/portfolio' && method === 'GET') {
      return Promise.resolve(jsonResponse({
        cashAccounts: [{
          currency: 'VND',
          balance: 97_085_000,
          availableBalance: 97_085_000,
          updatedAt: '2026-07-12T09:00:00Z',
        }],
        holdings: [{
          symbol: 'HPG',
          boardId: 'G1',
          quantity: 100,
          availableQuantity: 100,
          pendingReceiveQuantity: 0,
          averageCost: 29_150,
          lastPrice: 29_150,
          marketValue: 2_915_000,
          costValue: 2_915_000,
          unrealizedPnL: 0,
          updatedAt: '2026-07-12T09:00:00Z',
        }],
        totalCash: 97_085_000,
        totalAvailableCash: 97_085_000,
        totalMarketValue: 2_915_000,
        totalEquity: 100_000_000,
        totalUnrealizedPnL: 0,
        updatedAt: '2026-07-12T09:00:00Z',
      }));
    }

    return Promise.resolve(new Response('Not found', { status: 404, statusText: 'Not Found' }));
  };
}

function createSimulatedOrder(override: Partial<{
  averageFillPrice: number | null;
  executions: unknown[];
  filledQuantity: number;
  id: string;
  limitPrice: number | null;
  orderType: 'LO' | 'MTL';
  status: 'New' | 'Filled' | 'Cancelled' | 'Rejected';
}> = {}) {
  return {
    id: override.id ?? 'c9fd5bb5-5bdb-4cd8-8ecb-3aeaf011dd40',
    symbol: 'HPG',
    boardId: 'G1',
    side: 'Buy',
    orderType: override.orderType ?? 'MTL',
    quantity: 100,
    limitPrice: override.limitPrice ?? null,
    status: override.status ?? 'Filled',
    filledQuantity: override.filledQuantity ?? 100,
    averageFillPrice: override.averageFillPrice === undefined ? 29_150 : override.averageFillPrice,
    createdAt: '2026-07-12T09:00:00Z',
    updatedAt: '2026-07-12T09:00:00Z',
    executions: override.executions ?? [{
      id: '2f059f94-a486-4513-a498-6fc262bf8331',
      quantity: 100,
      price: 29_150,
      grossAmount: 2_915_000,
      executedAt: '2026-07-12T09:00:00Z',
    }],
  };
}

function createDemoSession() {
  return {
    accessToken: 'test-token',
    tokenType: 'Bearer',
    expiresAt: '2027-07-12T10:00:00Z',
    user: {
      id: '6b4e73e5-53f6-421c-bcec-24c2dfbcd4a5',
      email: 'demo@investview.local',
      displayName: 'InvestView Demo',
    },
  };
}

function jsonResponse(body: unknown, init?: ResponseInit) {
  return new Response(JSON.stringify(body), {
    status: init?.status ?? 200,
    statusText: init?.statusText,
    headers: init?.headers ?? { 'Content-Type': 'application/json' },
  });
}
