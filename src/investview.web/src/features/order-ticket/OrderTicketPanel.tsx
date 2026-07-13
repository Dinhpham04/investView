import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { SettingsIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { TooltipProvider } from '@/components/ui/tooltip';
import { placeOrder } from '../../shared/api/tradingApi';
import type { MarketQuote } from '../../shared/types/market';
import type { OrderSide, PlaceOrderRequest, SimulatedOrder } from '../../shared/types/trading';
import { useDemoSession } from '../auth/useDemoSession';
import { usePortfolio } from '../portfolio/usePortfolio';
import { formatMoney } from '../trading/tradingFormatters';
import type { SymbolDetailSelection } from '../symbol-detail/useSymbolDetailQueries';
import { OrderAccountArea } from './OrderAccountArea';
import type { AccountTab } from './OrderAccountArea';
import { FieldRow, QuoteHeader, StepperInput } from './OrderTicketControls';

type OrderTicketPanelProps = {
  liveQuote: MarketQuote | null;
  selection: SymbolDetailSelection | null;
};

type QuickOrderType = 'MTL' | 'ATO' | 'ATC';

export function OrderTicketPanel({ liveQuote, selection }: OrderTicketPanelProps) {
  const queryClient = useQueryClient();
  const { session, status } = useDemoSession();
  const { orders, ordersQuery, portfolio, portfolioQuery } = usePortfolio();
  const [quantity, setQuantity] = useState('');
  const [limitPrice, setLimitPrice] = useState('');
  const [quickOrderType, setQuickOrderType] = useState<QuickOrderType>('ATO');
  const [isAutomaticPrice, setIsAutomaticPrice] = useState(false);
  const [rememberAuthentication, setRememberAuthentication] = useState(true);
  const [activeAccountTab, setActiveAccountTab] = useState<AccountTab>('orders');
  const [lastOrder, setLastOrder] = useState<SimulatedOrder | null>(null);
  const accessToken = session?.accessToken ?? null;
  const selectedSymbol = selection?.symbol ?? null;
  const selectedBoardId = selection?.boardId ?? null;
  const referencePrice = liveQuote?.lastPrice ?? liveQuote?.referencePrice ?? null;
  const parsedQuantity = Number(quantity);
  const enteredPrice = limitPrice.trim() === '' ? null : Number(limitPrice);
  const parsedLimitPrice = normalizeOrderPrice(enteredPrice, referencePrice);
  const visibleOrders = useMemo(
    () => lastOrder == null || orders.some((order) => order.id === lastOrder.id) ? orders : [lastOrder, ...orders],
    [lastOrder, orders],
  );
  const validationMessage = useMemo(() => {
    if (selection == null) {
      return 'Chọn mã trên bảng giá để đặt lệnh mô phỏng.';
    }

    if (!Number.isInteger(parsedQuantity) || parsedQuantity <= 0) {
      return 'Khối lượng phải là số nguyên dương.';
    }

    if (enteredPrice != null && (!Number.isFinite(enteredPrice) || enteredPrice <= 0)) {
      return 'Giá giới hạn phải lớn hơn 0.';
    }

    return null;
  }, [enteredPrice, parsedQuantity, selection]);
  const orderMutation = useMutation({
    mutationFn: (request: PlaceOrderRequest) => {
      if (accessToken == null) {
        throw new Error('Bạn cần đăng nhập tài khoản demo.');
      }

      return placeOrder(accessToken, request);
    },
    onSuccess: (order) => {
      setLastOrder(order);
      void queryClient.invalidateQueries({ queryKey: ['portfolio'] });
      void queryClient.invalidateQueries({ queryKey: ['orders'] });
    },
  });
  const effectivePrice = parsedLimitPrice ?? referencePrice;
  const estimatedValue = Number.isInteger(parsedQuantity) && parsedQuantity > 0 && effectivePrice != null
    ? parsedQuantity * effectivePrice
    : null;
  const isOrderDisabled = status !== 'authenticated'
    || orderMutation.isPending
    || validationMessage != null;

  useEffect(() => {
    setLastOrder(null);
  }, [selectedBoardId, selectedSymbol]);

  const handlePlaceOrder = async (side: OrderSide) => {
    if (selection == null || validationMessage != null) {
      return;
    }

    await orderMutation.mutateAsync({
      symbol: selection.symbol,
      boardId: selection.boardId,
      side,
      quantity: parsedQuantity,
      limitPrice: parsedLimitPrice,
    });
  };

  const handleAutomaticPrice = (checked: boolean) => {
    setIsAutomaticPrice(checked);

    if (checked && referencePrice != null) {
      setLimitPrice(String(toDisplayPrice(referencePrice)));
    }
  };

  const handleQuickOrderType = (orderType: QuickOrderType) => {
    setQuickOrderType(orderType);
    setIsAutomaticPrice(false);
    setLimitPrice('');
  };

  return (
    <TooltipProvider delayDuration={250}>
      <section
        aria-label="Phieu lenh mo phong"
        className="flex h-full min-h-0 flex-col overflow-hidden bg-[#1b1828] text-[12px] text-[#ddd9e5]"
      >
        <QuoteHeader liveQuote={liveQuote} selection={selection} />

      <form
        className="shrink-0 border-b border-[#393548] pb-2"
        onSubmit={(event) => event.preventDefault()}
      >
        <FieldRow label="Tài khoản đặt lệnh">
          <div className="flex items-center gap-2">
            <Select
              disabled={session == null}
              value={session == null ? 'guest' : 'demo'}
            >
              <SelectTrigger
              aria-label="Tài khoản đặt lệnh"
                className="h-[26px] min-w-0 flex-1 rounded border-[#4a465d] bg-[#211e30] px-2 text-[12px] text-[#d9d6e1]"
                size="sm"
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent className="border-[#4a465d] bg-[#211e30] text-[#d9d6e1]">
                <SelectItem value="guest">Chưa đăng nhập</SelectItem>
                <SelectItem value="demo">Tài khoản demo</SelectItem>
              </SelectContent>
            </Select>
            <Button
              aria-label="Cài đặt tài khoản"
              className="size-6 rounded text-[#b9b5c3] hover:bg-white/5 hover:text-white"
              size="icon-xs"
              type="button"
              variant="ghost"
            >
              <SettingsIcon className="size-3.5" />
            </Button>
          </div>
        </FieldRow>

        <FieldRow label="Sức mua" withInfo>
          <strong className="text-[12px] text-[#e8e5ec]">
            {portfolio == null ? '-' : formatMoney(portfolio.totalAvailableCash)}
          </strong>
        </FieldRow>

        <FieldRow label="Giá tự động" withInfo>
          <Switch
            aria-label="Giá tự động"
            checked={isAutomaticPrice}
            className="data-checked:bg-[#605a8a] data-unchecked:bg-[#55516b]"
            onCheckedChange={handleAutomaticPrice}
          />
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

        <FieldRow label="Giá (x1000 VNĐ)">
          <StepperInput
            ariaLabel="Giá (x1000 VNĐ)"
            id="order-limit-price"
            step={0.05}
            value={limitPrice}
            onChange={(value) => {
              setLimitPrice(value);
              setIsAutomaticPrice(false);
            }}
          />
        </FieldRow>

        <FieldRow label="">
          <div aria-label="Loại lệnh nhanh" className="grid grid-cols-3 gap-2">
            {(['MTL', 'ATO', 'ATC'] as const).map((orderType) => (
              <Button
                className={`h-[21px] rounded bg-[#29253b] text-[12px] font-bold ${quickOrderType === orderType && limitPrice === '' ? 'text-white ring-1 ring-[#4b465d]' : 'text-[#c6c2ce]'}`}
                key={orderType}
                size="xs"
                type="button"
                variant="ghost"
                onClick={() => handleQuickOrderType(orderType)}
              >
                {orderType}
              </Button>
            ))}
          </div>
        </FieldRow>

        <FieldRow label="Giá trị">
          <strong className="text-[12px] text-[#e8e5ec]">
            {estimatedValue == null ? '-' : formatMoney(estimatedValue)}
          </strong>
        </FieldRow>

        <FieldRow label="Kiểu xác thực">
          <div className="flex min-w-0 items-center justify-between gap-2">
            <strong className="whitespace-nowrap text-[#e6e2ea]">Mã Smart OTP</strong>
            <label className="flex items-center gap-1.5 whitespace-nowrap font-semibold text-[#ddd9e5]">
              <Checkbox
                aria-label="Lưu xác thực"
                checked={rememberAuthentication}
                className="border-[#ed3b48] data-checked:border-[#ed3b48] data-checked:bg-[#ed3b48] data-checked:text-white"
                onCheckedChange={(checked) => setRememberAuthentication(checked === true)}
              />
              Lưu xác thực
            </label>
            <Select defaultValue="8h">
              <SelectTrigger
                aria-label="Thời gian lưu xác thực"
                className="h-[27px] w-[91px] rounded border-[#4a465d] bg-[#211e30] px-2 text-[12px] font-semibold text-white"
                size="sm"
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent className="border-[#4a465d] bg-[#211e30] text-[#d9d6e1]">
                <SelectItem value="8h">8 Giờ</SelectItem>
                <SelectItem value="4h">4 Giờ</SelectItem>
                <SelectItem value="session">Phiên này</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </FieldRow>

        {status === 'checking' ? (
          <p className="px-2 pb-1 text-[11px] text-[#aaa7b3]">Đang xác minh phiên đăng nhập...</p>
        ) : session == null ? (
          <p className="px-2 pb-1 text-[11px] text-[#aaa7b3]">
            Đăng nhập ở góc trên bên phải để đặt lệnh mô phỏng.
          </p>
        ) : validationMessage != null ? (
          <p className="px-2 pb-1 text-[11px] text-[#aaa7b3]">{validationMessage}</p>
        ) : null}

        <div className="grid grid-cols-2 gap-2 px-2 pt-1">
          <Button
            className="h-[30px] rounded bg-[#21ad83] text-[13px] font-bold text-white hover:bg-[#28bd90] disabled:cursor-not-allowed disabled:opacity-45"
            disabled={isOrderDisabled}
            size="sm"
            type="button"
            variant="default"
            onClick={() => void handlePlaceOrder('Buy').catch(() => undefined)}
          >
            {orderMutation.isPending ? 'Đang gửi...' : 'Mua'}
          </Button>
          <Button
            className="h-[30px] rounded bg-[#d81024] text-[13px] font-bold text-white hover:bg-[#e51a2e] disabled:cursor-not-allowed disabled:opacity-45"
            disabled={isOrderDisabled}
            size="sm"
            type="button"
            variant="destructive"
            onClick={() => void handlePlaceOrder('Sell').catch(() => undefined)}
          >
            {orderMutation.isPending ? 'Đang gửi...' : 'Bán'}
          </Button>
        </div>

        {orderMutation.error instanceof Error ? (
          <p className="px-2 pt-1 text-[11px] font-semibold text-[#ff4d62]" role="alert">
            {orderMutation.error.message}
          </p>
        ) : null}
      </form>

        <OrderAccountArea
          activeTab={activeAccountTab}
          orders={visibleOrders}
          ordersLoading={session != null && ordersQuery.isPending}
          portfolio={portfolio}
          portfolioError={portfolioQuery.error}
          portfolioLoading={session != null && portfolioQuery.isPending}
          onTabChange={setActiveAccountTab}
        />
      </section>
    </TooltipProvider>
  );
}

function normalizeOrderPrice(value: number | null, referencePrice: number | null) {
  if (value == null) {
    return null;
  }

  return referencePrice != null && Math.abs(referencePrice) >= 1000 && Math.abs(value) < 1000
    ? value * 1000
    : value;
}

function toDisplayPrice(value: number) {
  return Math.abs(value) >= 1000 ? value / 1000 : value;
}
