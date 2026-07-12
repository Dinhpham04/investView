import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { placeOrder } from '../../shared/api/tradingApi';
import type { MarketQuote } from '../../shared/types/market';
import type { OrderSide, PlaceOrderRequest, SimulatedOrder } from '../../shared/types/trading';
import { useDemoSession } from '../auth/useDemoSession';
import { formatOrderPrice, formatQuantity } from '../trading/tradingFormatters';
import type { SymbolDetailSelection } from '../symbol-detail/useSymbolDetailQueries';

type OrderTicketPanelProps = {
  liveQuote: MarketQuote | null;
  selection: SymbolDetailSelection | null;
};

export function OrderTicketPanel({ liveQuote, selection }: OrderTicketPanelProps) {
  const queryClient = useQueryClient();
  const {
    error: loginError,
    isLoggingIn,
    login,
    session,
  } = useDemoSession();
  const [side, setSide] = useState<OrderSide>('Buy');
  const [quantity, setQuantity] = useState('100');
  const [orderType, setOrderType] = useState<'market' | 'limit'>('market');
  const [limitPrice, setLimitPrice] = useState('');
  const [lastOrder, setLastOrder] = useState<SimulatedOrder | null>(null);
  const accessToken = session?.accessToken ?? null;
  const selectedSymbol = selection?.symbol ?? null;
  const selectedBoardId = selection?.boardId ?? null;
  const referencePrice = liveQuote?.lastPrice ?? liveQuote?.referencePrice ?? null;
  const parsedQuantity = Number(quantity);
  const parsedLimitPrice = orderType === 'limit' ? Number(limitPrice) : null;
  const validationMessage = useMemo(() => {
    if (selection == null) {
      return 'Chon ma tren bang gia de dat lenh mo phong.';
    }

    if (!Number.isInteger(parsedQuantity) || parsedQuantity <= 0) {
      return 'Khoi luong phai la so nguyen duong.';
    }

    if (orderType === 'limit' && (parsedLimitPrice == null || !Number.isFinite(parsedLimitPrice) || parsedLimitPrice <= 0)) {
      return 'Gia gioi han phai lon hon 0.';
    }

    return null;
  }, [orderType, parsedLimitPrice, parsedQuantity, selection]);
  const orderMutation = useMutation({
    mutationFn: (request: PlaceOrderRequest) => {
      if (accessToken == null) {
        throw new Error('Demo login is required.');
      }

      return placeOrder(accessToken, request);
    },
    onSuccess: (order) => {
      setLastOrder(order);
      void queryClient.invalidateQueries({ queryKey: ['portfolio'] });
      void queryClient.invalidateQueries({ queryKey: ['orders'] });
    },
  });
  const error = orderMutation.error ?? loginError;

  useEffect(() => {
    setLastOrder(null);
  }, [selectedBoardId, selectedSymbol]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (selection == null || validationMessage != null) {
      return;
    }

    await orderMutation.mutateAsync({
      symbol: selection.symbol,
      boardId: selection.boardId,
      side,
      quantity: parsedQuantity,
      limitPrice: orderType === 'limit' ? parsedLimitPrice : null,
    });
  };

  return (
    <section className="border-t border-market-border bg-market-surface px-3 py-2" aria-label="Phieu lenh mo phong">
      <form className="flex flex-wrap items-end gap-2" onSubmit={(event) => void handleSubmit(event).catch(() => undefined)}>
        <div className="min-w-36">
          <p className="text-[10px] font-bold uppercase tracking-normal text-state-warning">Lenh mo phong</p>
          <p className="truncate text-[13px] font-extrabold text-market-text">
            {selectedSymbol == null ? 'Chua chon ma' : `${selectedSymbol} ${selectedBoardId}`}
          </p>
          <p className="text-[11px] font-semibold text-market-text-muted">
            Gia tham chieu: {formatOrderPrice(referencePrice)}
          </p>
        </div>

        {session == null ? (
          <button
            className="h-8 border border-market-border-strong bg-market-surface-2 px-3 text-[12px] font-bold text-market-text hover:border-focus-ring disabled:text-market-text-subtle"
            disabled={isLoggingIn}
            type="button"
            onClick={() => {
              void login().catch(() => undefined);
            }}
          >
            {isLoggingIn ? 'Dang nhap...' : 'Dang nhap demo'}
          </button>
        ) : (
          <>
            <div className="flex h-8 overflow-hidden rounded border border-market-border" aria-label="Chieu lenh">
              <button
                className={`w-16 text-[12px] font-extrabold ${side === 'Buy' ? 'bg-price-up text-[#05110a]' : 'bg-market-surface-2 text-market-text-muted'}`}
                type="button"
                onClick={() => setSide('Buy')}
              >
                Mua
              </button>
              <button
                className={`w-16 border-l border-market-border text-[12px] font-extrabold ${side === 'Sell' ? 'bg-price-down text-white' : 'bg-market-surface-2 text-market-text-muted'}`}
                type="button"
                onClick={() => setSide('Sell')}
              >
                Ban
              </button>
            </div>

            <div>
              <label className="mb-1 block text-[10px] font-bold uppercase text-market-text-muted" htmlFor="order-quantity">
                Khoi luong
              </label>
              <input
                className="h-8 w-24 rounded border border-market-border bg-market-surface-2 px-2 text-right text-[12px] font-bold text-market-text outline-none focus:border-focus-ring"
                id="order-quantity"
                min={1}
                onChange={(event) => setQuantity(event.target.value)}
                step={1}
                type="number"
                value={quantity}
              />
            </div>

            <div className="flex h-8 overflow-hidden rounded border border-market-border" aria-label="Loai lenh">
              <button
                className={`w-16 text-[12px] font-bold ${orderType === 'market' ? 'bg-market-border-strong text-market-text' : 'bg-market-surface-2 text-market-text-muted'}`}
                type="button"
                onClick={() => setOrderType('market')}
              >
                MP
              </button>
              <button
                className={`w-16 border-l border-market-border text-[12px] font-bold ${orderType === 'limit' ? 'bg-market-border-strong text-market-text' : 'bg-market-surface-2 text-market-text-muted'}`}
                type="button"
                onClick={() => setOrderType('limit')}
              >
                LO
              </button>
            </div>

            <div>
              <label className="mb-1 block text-[10px] font-bold uppercase text-market-text-muted" htmlFor="order-limit-price">
                Gia LO
              </label>
              <input
                className="h-8 w-28 rounded border border-market-border bg-market-surface-2 px-2 text-right text-[12px] font-bold text-market-text outline-none disabled:text-market-text-subtle focus:border-focus-ring"
                disabled={orderType === 'market'}
                id="order-limit-price"
                min={1}
                onChange={(event) => setLimitPrice(event.target.value)}
                placeholder="29150"
                step="0.01"
                type="number"
                value={limitPrice}
              />
            </div>

            <button
              className="h-8 border border-market-border-strong bg-market-surface-2 px-3 text-[12px] font-extrabold text-market-text hover:border-focus-ring disabled:text-market-text-subtle"
              disabled={orderMutation.isPending || validationMessage != null}
              type="submit"
            >
              {orderMutation.isPending ? 'Dang gui...' : 'Dat lenh mo phong'}
            </button>
          </>
        )}

        {lastOrder != null ? (
          <div className="min-w-0 text-[11px] font-semibold text-market-text-muted" role="status">
            {lastOrder.symbol} {lastOrder.status} {formatQuantity(lastOrder.filledQuantity || lastOrder.quantity)}
            {' '}@ {formatOrderPrice(lastOrder.averageFillPrice ?? lastOrder.limitPrice)}
          </div>
        ) : validationMessage != null ? (
          <p className="text-[11px] font-semibold text-market-text-muted">{validationMessage}</p>
        ) : null}
      </form>

      {error instanceof Error ? (
        <p className="mt-1 text-[11px] font-semibold text-state-error" role="alert">
          {error.message}
        </p>
      ) : null}
    </section>
  );
}
