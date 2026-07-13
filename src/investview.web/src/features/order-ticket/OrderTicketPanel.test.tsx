import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { OrderTicketPanel } from './OrderTicketPanel';
import { renderWithQueryClient } from '../../test/renderWithQueryClient';
import type { MarketQuote } from '../../shared/types/market';
import { DemoSessionControls } from '../auth/DemoSessionControls';

describe('OrderTicketPanel', () => {
  afterEach(() => {
    window.localStorage?.clear();
    vi.unstubAllGlobals();
  });

  it('logs in and submits a simulated market order for the selected symbol', async () => {
    const fetchMock = vi.fn(createOrderTicketFetch());
    vi.stubGlobal('fetch', fetchMock);

    renderWithQueryClient(<><DemoSessionControls /><OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    await screen.findByRole('button', { name: 'Mua' });
    fireEvent.change(screen.getByLabelText('Khối lượng'), { target: { value: '100' } });
    fireEvent.click(screen.getByRole('button', { name: 'Mua' }));

    await waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/orders',
        expect.objectContaining({
          body: JSON.stringify({
            symbol: 'HPG',
            boardId: 'G1',
            side: 'Buy',
            quantity: 100,
            limitPrice: null,
          }),
          headers: expect.objectContaining({ Authorization: 'Bearer test-token' }),
          method: 'POST',
        }),
      ),
    );
    expect(await screen.findByRole('row', { name: 'HPG Mua Đã khớp' })).toBeInTheDocument();
  });

  it('disables submit when quantity is invalid', async () => {
    vi.stubGlobal('fetch', vi.fn(createOrderTicketFetch()));

    renderWithQueryClient(<><DemoSessionControls /><OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    await screen.findByRole('button', { name: 'Mua' });
    fireEvent.change(screen.getByLabelText('Khối lượng'), { target: { value: '0' } });

    expect(screen.getByText('Khối lượng phải là số nguyên dương.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Mua' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Bán' })).toBeDisabled();
  });

  it('renders the reference ticket structure and keeps order entry gated behind login', () => {
    renderWithQueryClient(<OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} />);

    expect(screen.getByText('HPG')).toBeInTheDocument();
    expect(screen.getByText('(HOSE)')).toBeInTheDocument();
    expect(screen.getByText('Tài khoản đặt lệnh')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Tài khoản đặt lệnh' })).toHaveAttribute('data-slot', 'select-trigger');
    expect(screen.getByText('Sức mua')).toBeInTheDocument();
    expect(screen.getByText('Giá tự động')).toBeInTheDocument();
    expect(screen.getByRole('switch', { name: 'Giá tự động' })).toHaveAttribute('data-slot', 'switch');
    expect(screen.getByText('Giá (x1000 VNĐ)')).toBeInTheDocument();
    expect(screen.getByLabelText('Khối lượng')).toHaveAttribute('data-slot', 'input-group-control');
    expect(screen.getByLabelText('Khối lượng')).toHaveAttribute('type', 'text');
    expect(screen.getByLabelText('Khối lượng')).toHaveAttribute('inputmode', 'decimal');
    expect(screen.getByLabelText('Khối lượng').closest('[data-slot="input-group"]')).toBeInTheDocument();
    expect(screen.getByLabelText('Giá (x1000 VNĐ)')).toHaveAttribute('data-slot', 'input-group-control');
    expect(screen.getByLabelText('Giá (x1000 VNĐ)')).toHaveAttribute('type', 'text');
    expect(screen.getByLabelText('Giá (x1000 VNĐ)')).toHaveAttribute('inputmode', 'decimal');
    expect(screen.getByRole('button', { name: 'MTL' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ATO' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ATC' })).toBeInTheDocument();
    expect(screen.getByText('Kiểu xác thực')).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Lưu xác thực' })).toHaveAttribute('data-slot', 'checkbox');
    expect(screen.getByRole('tab', { name: 'Sổ lệnh' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Sổ lệnh điều kiện' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Danh mục' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Tài sản' })).toBeInTheDocument();
    expect(screen.getByText('Đăng nhập ở góc trên bên phải để đặt lệnh mô phỏng.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Mua' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Mua' })).toHaveAttribute('data-slot', 'button');
    expect(screen.getByRole('button', { name: 'Bán' })).toBeDisabled();
    expect(screen.getByText('Không tìm thấy lệnh nào')).toBeInTheDocument();

    const conditionalOrdersTab = screen.getByRole('tab', { name: 'Sổ lệnh điều kiện' });
    fireEvent.mouseDown(conditionalOrdersTab, { button: 0, ctrlKey: false });
    fireEvent.click(conditionalOrdersTab);
    expect(screen.getByText('Chưa có lệnh điều kiện')).toBeInTheDocument();
  });

  it('renders portfolio assets as a compact account dashboard', async () => {
    vi.stubGlobal('fetch', vi.fn(createOrderTicketFetch()));

    renderWithQueryClient(<><DemoSessionControls /><OrderTicketPanel liveQuote={quote} selection={{ symbol: 'HPG', boardId: 'G1' }} /></>);

    fireEvent.click(screen.getByRole('button', { name: 'Đăng nhập demo' }));
    const assetsTab = await screen.findByRole('tab', { name: 'Tài sản' });
    fireEvent.mouseDown(assetsTab, { button: 0, ctrlKey: false });
    fireEvent.click(assetsTab);

    const assetsRegion = await screen.findByRole('region', { name: 'Tài sản tài khoản mô phỏng' });
    expect(within(assetsRegion).getByText('InvestView Demo')).toBeInTheDocument();
    expect(within(assetsRegion).getByText('Tổng tài sản')).toBeInTheDocument();
    expect(within(assetsRegion).getByText('100,000,000 VND')).toBeInTheDocument();
    expect(within(assetsRegion).getAllByText('Tiền mặt').length).toBeGreaterThan(0);
    expect(within(assetsRegion).getAllByText('97,085,000 VND').length).toBeGreaterThan(0);
    expect(within(assetsRegion).getByText('Giá trị CK')).toBeInTheDocument();
    expect(within(assetsRegion).getAllByText('2,915,000 VND').length).toBeGreaterThan(0);
    expect(within(assetsRegion).getByText('Lãi/lỗ tạm tính')).toBeInTheDocument();
    expect(within(assetsRegion).getByText('Danh mục nắm giữ')).toBeInTheDocument();
    expect(within(assetsRegion).getByText('HPG')).toBeInTheDocument();
    expect(within(assetsRegion).getByText(/100 cp/)).toBeInTheDocument();
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

function createOrderTicketFetch() {
  return (input: RequestInfo | URL, init?: RequestInit) => {
    const url = input.toString();
    const method = init?.method ?? 'GET';

    if (url === '/api/auth/demo-login' && method === 'POST') {
      return Promise.resolve(jsonResponse(createDemoSession()));
    }

    if (url === '/api/orders' && method === 'POST') {
      return Promise.resolve(jsonResponse({
        id: 'c9fd5bb5-5bdb-4cd8-8ecb-3aeaf011dd40',
        symbol: 'HPG',
        boardId: 'G1',
        side: 'Buy',
        quantity: 100,
        limitPrice: null,
        status: 'Filled',
        filledQuantity: 100,
        averageFillPrice: 29_150,
        createdAt: '2026-07-12T09:00:00Z',
        updatedAt: '2026-07-12T09:00:00Z',
        executions: [{
          id: '2f059f94-a486-4513-a498-6fc262bf8331',
          quantity: 100,
          price: 29_150,
          grossAmount: 2_915_000,
          executedAt: '2026-07-12T09:00:00Z',
        }],
      }, { status: 201 }));
    }

    if (url === '/api/orders' && method === 'GET') {
      return Promise.resolve(jsonResponse([]));
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
    headers: { 'Content-Type': 'application/json' },
  });
}
