export type OrderSide = 'Buy' | 'Sell';

export type OrderStatus = 'New' | 'Filled' | 'Cancelled' | 'Rejected';

export type OrderExecution = {
  id: string;
  quantity: number;
  price: number;
  grossAmount: number;
  executedAt: string;
};

export type SimulatedOrder = {
  id: string;
  symbol: string;
  boardId: string;
  side: OrderSide;
  quantity: number;
  limitPrice: number | null;
  status: OrderStatus;
  filledQuantity: number;
  averageFillPrice: number | null;
  createdAt: string;
  updatedAt: string;
  executions: OrderExecution[];
};

export type PlaceOrderRequest = {
  symbol: string;
  boardId: string;
  side: OrderSide;
  quantity: number;
  limitPrice: number | null;
};

export type CashAccount = {
  currency: string;
  balance: number;
  availableBalance: number;
  updatedAt: string;
};

export type HoldingPosition = {
  symbol: string;
  boardId: string;
  quantity: number;
  availableQuantity: number;
  averageCost: number;
  lastPrice: number;
  marketValue: number;
  costValue: number;
  unrealizedPnL: number;
  updatedAt: string;
};

export type PortfolioSnapshot = {
  cashAccounts: CashAccount[];
  holdings: HoldingPosition[];
  totalCash: number;
  totalAvailableCash: number;
  totalMarketValue: number;
  totalEquity: number;
  totalUnrealizedPnL: number;
  updatedAt: string;
};
