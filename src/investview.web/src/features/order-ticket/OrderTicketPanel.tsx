import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { TooltipProvider } from '@/components/ui/tooltip';
import { cancelOrder, placeOrder } from '../../shared/api/tradingApi';
import type { MarketQuote, MarketSessionUpdate } from '../../shared/types/market';
import type { OrderSide, OrderType, PlaceOrderRequest, SimulatedOrder } from '../../shared/types/trading';
import { formatPrice, formatQuantity } from '../market-board/marketBoardFormatters';
import { useDemoSession } from '../auth/useDemoSession';
import { usePortfolio } from '../portfolio/usePortfolio';
import { formatMoney } from '../trading/tradingFormatters';
import type { SymbolDetailSelection } from '../symbol-detail/useSymbolDetailQueries';
import { OrderAccountArea } from './OrderAccountArea';
import type { AccountTab } from './OrderAccountArea';
import { FieldRow, QuoteHeader, StepperInput } from './OrderTicketControls';

type OrderTicketPanelProps = {
  preset?: OrderTicketPreset | null;
  liveQuote: MarketQuote | null;
  marketSession?: MarketSessionUpdate | null;
  selection: SymbolDetailSelection | null;
};

type TicketOrderType = Extract<OrderType, 'LO' | 'MTL'>;

export type OrderTicketPreset = {
  id: number;
  limitPrice: number | null;
  orderType: TicketOrderType;
  quantity?: number;
};

const orderTypeOptions: Array<{ id: TicketOrderType; hint: string }> = [
  {
    id: 'MTL',
    hint: 'Lệnh thị trường khớp theo giá thị trường hiện tại trong mô phỏng.',
  },
  {
    id: 'LO',
    hint: 'Lệnh LO sẽ chờ khớp nếu giá thị trường chưa thỏa điều kiện giá đặt.',
  },
];

export function OrderTicketPanel({ liveQuote, marketSession = null, preset = null, selection }: OrderTicketPanelProps) {
  const queryClient = useQueryClient();
  const { session, status } = useDemoSession();
  const { orders, ordersQuery, portfolio, portfolioQuery } = usePortfolio();
  const [quantity, setQuantity] = useState('');
  const [limitPrice, setLimitPrice] = useState('');
  const [orderType, setOrderType] = useState<TicketOrderType>('MTL');
  const [activeAccountTab, setActiveAccountTab] = useState<AccountTab>('orders');
  const [lastOrder, setLastOrder] = useState<SimulatedOrder | null>(null);
  const [editingOrder, setEditingOrder] = useState<SimulatedOrder | null>(null);
  const accessToken = session?.accessToken ?? null;
  const effectiveSelection = useMemo(
    () => editingOrder == null
      ? selection
      : { boardId: editingOrder.boardId, symbol: editingOrder.symbol },
    [editingOrder, selection],
  );
  const selectedSymbol = effectiveSelection?.symbol ?? null;
  const selectedBoardId = effectiveSelection?.boardId ?? null;
  const marketPrice = liveQuote != null && liveQuote.symbol === selectedSymbol && liveQuote.boardId === selectedBoardId
    ? liveQuote.lastPrice ?? liveQuote.referencePrice ?? null
    : null;
  const parsedQuantity = Number(quantity);
  const enteredPrice = limitPrice.trim() === '' ? null : Number(limitPrice.replace(',', '.'));
  const parsedLimitPrice = orderType === 'LO' ? normalizeOrderPrice(enteredPrice, marketPrice) : null;
  const selectedHolding = useMemo(() => {
    if (portfolio == null || effectiveSelection == null) {
      return null;
    }

    return portfolio.holdings.find((holding) =>
      holding.symbol === effectiveSelection.symbol &&
      holding.boardId === effectiveSelection.boardId,
    ) ?? null;
  }, [effectiveSelection, portfolio]);
  const visibleOrders = useMemo(
    () => lastOrder == null || orders.some((order) => order.id === lastOrder.id) ? orders : [lastOrder, ...orders],
    [lastOrder, orders],
  );
  const effectivePrice = parsedLimitPrice ?? marketPrice;
  const estimatedValue = Number.isInteger(parsedQuantity) && parsedQuantity > 0 && effectivePrice != null
    ? parsedQuantity * effectivePrice
    : null;
  const marketSessionValidationMessage = getMarketSessionValidationMessage(marketSession);
  const baseValidationMessage = useMemo(() => {
    if (effectiveSelection == null) {
      return 'Chọn mã trên bảng giá để đặt lệnh mô phỏng.';
    }

    if (marketSessionValidationMessage != null) {
      return marketSessionValidationMessage;
    }

    if (!Number.isInteger(parsedQuantity) || parsedQuantity <= 0) {
      return 'Khối lượng phải là số nguyên dương.';
    }

    if (orderType === 'LO' && (enteredPrice == null || !Number.isFinite(enteredPrice) || enteredPrice <= 0)) {
      return 'Giá giới hạn phải lớn hơn 0.';
    }

    if (orderType === 'MTL' && marketPrice == null) {
      return 'Chưa có giá thị trường để đặt lệnh MTL.';
    }

    return null;
  }, [effectiveSelection, enteredPrice, marketPrice, marketSessionValidationMessage, orderType, parsedQuantity]);
  const buyValidationMessage = useMemo(() => {
    if (baseValidationMessage != null) {
      return baseValidationMessage;
    }

    if (estimatedValue != null && portfolio != null && estimatedValue > portfolio.totalAvailableCash) {
      return 'Sức mua không đủ cho lệnh này.';
    }

    return null;
  }, [baseValidationMessage, estimatedValue, portfolio]);
  const sellValidationMessage = useMemo(() => {
    if (baseValidationMessage != null) {
      return baseValidationMessage;
    }

    if (selectedHolding == null || parsedQuantity > selectedHolding.availableQuantity) {
      return 'Khối lượng bán vượt quá số lượng có thể bán.';
    }

    return null;
  }, [baseValidationMessage, parsedQuantity, selectedHolding]);
  const orderHint = orderTypeOptions.find((option) => option.id === orderType)?.hint ?? '';
  const orderMutation = useMutation({
    mutationFn: (request: PlaceOrderRequest) => {
      if (accessToken == null) {
        throw new Error('Bạn cần đăng nhập tài khoản demo.');
      }

      return placeOrder(accessToken, request);
    },
    onSuccess: (order) => {
      setLastOrder(order);
      setEditingOrder(null);
      void queryClient.invalidateQueries({ queryKey: ['portfolio'] });
      void queryClient.invalidateQueries({ queryKey: ['orders'] });
    },
  });
  const cancelOrderMutation = useMutation({
    mutationFn: (order: SimulatedOrder) => {
      if (accessToken == null) {
        throw new Error('Bạn cần đăng nhập tài khoản demo.');
      }

      return cancelOrder(accessToken, order.id);
    },
    onSuccess: (order) => {
      setLastOrder(order);
      if (editingOrder?.id === order.id) {
        setEditingOrder(null);
      }
      void queryClient.invalidateQueries({ queryKey: ['portfolio'] });
      void queryClient.invalidateQueries({ queryKey: ['orders'] });
    },
  });
  const isBuyDisabled = status !== 'authenticated'
    || orderMutation.isPending
    || buyValidationMessage != null;
  const isSellDisabled = status !== 'authenticated'
    || orderMutation.isPending
    || sellValidationMessage != null;

  useEffect(() => {
    setLastOrder(null);
    setEditingOrder(null);
  }, [selectedBoardId, selectedSymbol]);

  useEffect(() => {
    if (preset == null) {
      return;
    }

    setOrderType(preset.orderType);
    setLimitPrice(preset.orderType === 'LO' ? formatLimitPriceInput(preset.limitPrice, marketPrice) : '');
    if (preset.quantity != null) {
      setQuantity(String(preset.quantity));
    }
  }, [marketPrice, preset]);

  const handlePlaceOrder = async (side: OrderSide) => {
    const validationMessage = side === 'Buy' ? buyValidationMessage : sellValidationMessage;
    if (effectiveSelection == null || validationMessage != null) {
      return;
    }

    await orderMutation.mutateAsync({
      symbol: effectiveSelection.symbol,
      boardId: effectiveSelection.boardId,
      side,
      orderType,
      quantity: parsedQuantity,
      limitPrice: parsedLimitPrice,
    });
  };

  const handleEditOrder = (order: SimulatedOrder) => {
    if (order.status !== 'New' || !isTicketOrderType(order.orderType)) {
      return;
    }

    setEditingOrder(order);
    setOrderType(order.orderType);
    setQuantity(String(order.quantity));
    setLimitPrice(order.orderType === 'LO' ? formatLimitPriceInput(order.limitPrice, marketPrice) : '');
    setActiveAccountTab('orders');
  };

  const handleCancelOrder = async (order: SimulatedOrder) => {
    if (order.status !== 'New') {
      return;
    }

    await cancelOrderMutation.mutateAsync(order);
  };

  const handleOrderType = (nextOrderType: TicketOrderType) => {
    setOrderType(nextOrderType);
    if (nextOrderType === 'MTL') {
      setLimitPrice('');
    }
  };

  return (
    <TooltipProvider delayDuration={250}>
      <section
        aria-label="Phieu lenh mo phong"
        className="flex h-full min-h-0 flex-col overflow-hidden bg-[#1b1828] text-[12px] text-[#ddd9e5]"
      >
        <QuoteHeader liveQuote={liveQuote} marketSession={marketSession} selection={selection} />

        <form
          className="shrink-0 border-b border-[#393548] pb-2"
          onSubmit={(event) => event.preventDefault()}
        >
          <FieldRow label="Tài khoản">
            <Select
              disabled={session == null}
              value={session == null ? 'guest' : 'demo'}
            >
              <SelectTrigger
                aria-label="Tài khoản đặt lệnh"
                className="h-[26px] min-w-0 rounded border-[#4a465d] bg-[#211e30] px-2 text-[12px] text-[#d9d6e1]"
                size="sm"
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent className="border-[#4a465d] bg-[#211e30] text-[#d9d6e1]">
                <SelectItem value="guest">Chưa đăng nhập</SelectItem>
                <SelectItem value="demo">Tài khoản demo</SelectItem>
              </SelectContent>
            </Select>
          </FieldRow>

          <FieldRow label="Sức mua" withInfo>
            <strong className="text-[12px] text-[#e8e5ec]">
              {portfolio == null ? '-' : formatMoney(portfolio.totalAvailableCash)}
            </strong>
          </FieldRow>

          <FieldRow label="Đang nắm giữ">
            <strong className="text-[12px] text-[#e8e5ec]">
              {selectedHolding == null ? '0 cp' : `${formatQuantity(selectedHolding.quantity)} cp`}
            </strong>
          </FieldRow>

          <FieldRow label="Có thể bán">
            <strong className="text-[12px] text-[#e8e5ec]">
              {selectedHolding == null ? '0 cp' : `${formatQuantity(selectedHolding.availableQuantity)} cp`}
            </strong>
          </FieldRow>

          <FieldRow label="Loại lệnh">
            <div aria-label="Loại lệnh" className="grid grid-cols-2 gap-2">
              {orderTypeOptions.map((option) => (
                <Button
                  aria-pressed={orderType === option.id}
                  className={`h-[24px] rounded bg-[#29253b] text-[12px] font-bold ${orderType === option.id ? 'text-white ring-1 ring-[#4b465d]' : 'text-[#c6c2ce]'}`}
                  key={option.id}
                  size="xs"
                  type="button"
                  variant="ghost"
                  onClick={() => handleOrderType(option.id)}
                >
                  {option.id}
                </Button>
              ))}
            </div>
          </FieldRow>

          <FieldRow label="Khối lượng">
            <StepperInput
              ariaLabel="Khối lượng"
              id="order-quantity"
              step={100}
              value={quantity}
              onChange={setQuantity}
            />
          </FieldRow>

          <FieldRow label="Giá LO (x1000 VNĐ)">
            <StepperInput
              ariaLabel="Giá LO (x1000 VNĐ)"
              disabled={orderType !== 'LO'}
              id="order-limit-price"
              step={0.05}
              value={orderType === 'LO' ? limitPrice : ''}
              onChange={setLimitPrice}
            />
          </FieldRow>

          <FieldRow label="Giá thị trường">
            <strong className="text-[12px] text-[#e8e5ec]">
              {marketPrice == null ? '-' : formatPrice(marketPrice)}
            </strong>
          </FieldRow>

          <FieldRow label="Giá trị tạm tính">
            <strong className="text-[12px] text-[#e8e5ec]">
              {estimatedValue == null ? '-' : formatMoney(estimatedValue)}
            </strong>
          </FieldRow>

          <p className={`px-2 pb-1 text-[11px] ${baseValidationMessage == null ? 'text-[#aaa7b3]' : 'font-semibold text-[#ffbe45]'}`}>
            {status === 'checking'
              ? 'Đang xác minh phiên đăng nhập...'
              : session == null
                ? 'Đăng nhập ở góc trên bên phải để đặt lệnh mô phỏng.'
                : editingOrder != null
                  ? `Đang sửa lệnh ${editingOrder.symbol}: chỉnh thông tin rồi gửi lệnh mới nếu cần.`
                  : baseValidationMessage ?? orderHint}
          </p>

          <div className="grid grid-cols-2 gap-2 px-2 pt-1">
            <Button
              className="h-[30px] rounded bg-[#21ad83] text-[13px] font-bold text-white hover:bg-[#28bd90] disabled:cursor-not-allowed disabled:opacity-45"
              disabled={isBuyDisabled}
              size="sm"
              type="button"
              variant="default"
              onClick={() => void handlePlaceOrder('Buy').catch(() => undefined)}
            >
              {orderMutation.isPending ? 'Đang gửi...' : `Mua ${selectedSymbol ?? ''}`.trim()}
            </Button>
            <Button
              className="h-[30px] rounded bg-[#d81024] text-[13px] font-bold text-white hover:bg-[#e51a2e] disabled:cursor-not-allowed disabled:opacity-45"
              disabled={isSellDisabled}
              size="sm"
              type="button"
              variant="destructive"
              onClick={() => void handlePlaceOrder('Sell').catch(() => undefined)}
            >
              {orderMutation.isPending ? 'Đang gửi...' : `Bán ${selectedSymbol ?? ''}`.trim()}
            </Button>
          </div>

          {orderMutation.error instanceof Error ? (
            <p className="px-2 pt-1 text-[11px] font-semibold text-[#ff4d62]" role="alert">
              {formatOrderErrorMessage(orderMutation.error)}
            </p>
          ) : null}

          {cancelOrderMutation.error instanceof Error ? (
            <p className="px-2 pt-1 text-[11px] font-semibold text-[#ff4d62]" role="alert">
              {formatOrderErrorMessage(cancelOrderMutation.error)}
            </p>
          ) : null}
        </form>

        <OrderAccountArea
          activeTab={activeAccountTab}
          cancellingOrderId={cancelOrderMutation.variables?.id ?? null}
          orders={visibleOrders}
          ordersLoading={session != null && ordersQuery.isPending}
          portfolio={portfolio}
          portfolioError={portfolioQuery.error}
          portfolioLoading={session != null && portfolioQuery.isPending}
          onCancelOrder={(order) => void handleCancelOrder(order).catch(() => undefined)}
          onEditOrder={handleEditOrder}
          onTabChange={setActiveAccountTab}
        />
      </section>
    </TooltipProvider>
  );
}

function isTicketOrderType(orderType: OrderType): orderType is TicketOrderType {
  return orderType === 'LO' || orderType === 'MTL';
}

function normalizeOrderPrice(value: number | null, referencePrice: number | null) {
  if (value == null) {
    return null;
  }

  return referencePrice != null && Math.abs(referencePrice) >= 1000 && Math.abs(value) < 1000
    ? value * 1000
    : value;
}

function formatLimitPriceInput(value: number | null, referencePrice: number | null) {
  if (value == null) {
    return '';
  }

  const displayValue = referencePrice != null && Math.abs(referencePrice) >= 1000 && Math.abs(value) >= 1000
    ? value / 1000
    : value;

  return String(Number(displayValue.toFixed(2)));
}

function getMarketSessionValidationMessage(marketSession: MarketSessionUpdate | null) {
  if (marketSession == null) {
    return null;
  }

  if (!marketSession.isOpen) {
    return 'Ngoài giờ giao dịch, không thể đặt lệnh mô phỏng.';
  }

  if (!marketSession.isContinuous) {
    return 'Hiện chỉ hỗ trợ đặt lệnh mô phỏng trong phiên liên tục.';
  }

  return null;
}

function formatOrderErrorMessage(error: Error) {
  if (error.message === 'Market is not open for simulated orders.') {
    return 'Ngoài giờ giao dịch, không thể đặt lệnh mô phỏng.';
  }

  return error.message;
}
