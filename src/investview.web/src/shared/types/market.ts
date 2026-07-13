export type PriceLevel = {
  price: number | null;
  quantity: number | null;
};

export type MarketQuote = {
  symbol: string;
  boardId: string;
  marketId: string;
  displayName: string;
  referencePrice: number | null;
  ceilingPrice: number | null;
  floorPrice: number | null;
  lastPrice: number | null;
  change: number | null;
  changePercent: number | null;
  lastQuantity: number | null;
  expectedPrice?: number | null;
  expectedQuantity?: number | null;
  totalVolume: number | null;
  totalValue: number | null;
  foreignBuyVolume: number | null;
  foreignSellVolume: number | null;
  foreignRoom: number | null;
  openPrice: number | null;
  highPrice: number | null;
  lowPrice: number | null;
  bidLevels: PriceLevel[];
  askLevels: PriceLevel[];
  tradingStatus: string;
  updatedAt: string;
};

export type SymbolDetail = MarketQuote & {
  isin: string;
  productGroupId: string;
  securityGroupId: string;
  securityType: string;
  name: string;
  symbolAdminStatus: string;
  tradingMethodStatus: string;
  tradingSanctionStatus: string;
  listingDate: string | null;
  finalTradeDate: string | null;
  openInterestQuantity: number | null;
};

export type OhlcBar = {
  symbol: string;
  resolution: string;
  time: string;
  open: number | null;
  high: number | null;
  low: number | null;
  close: number | null;
  volume: number | null;
};

export type MarketOhlcUpdate = OhlcBar & {
  type: string;
  isClosed: boolean;
  updatedAt: string;
};

export type MarketSessionUpdate = {
  marketId: string;
  boardId: string;
  productGroupId: string;
  eventId: string;
  tradingSessionId: string;
  updatedAt: string;
  phase: string;
  label: string;
  isOpen: boolean;
  isAuction: boolean;
  isContinuous: boolean;
  isPutThrough: boolean;
  isAfterHours: boolean;
  source: string;
};

export type MarketTrade = {
  symbol: string;
  boardId: string;
  time: string;
  price: number | null;
  change: number | null;
  changePercent: number | null;
  quantity: number | null;
  totalVolume: number | null;
  totalValue: number | null;
  side: string;
};

export type MarketTradeUpdate = MarketTrade;

export type MarketIndex = {
  indexName: string;
  value: number | null;
  change: number | null;
  changePercent: number | null;
  referenceValue: number | null;
  highValue: number | null;
  lowValue: number | null;
  totalVolume: number | null;
  totalValue: number | null;
  upCount: number | null;
  downCount: number | null;
  noChangeCount: number | null;
  ceilingCount: number | null;
  floorCount: number | null;
  marketId: string;
  tradingSessionId: string;
  updatedAt: string;
};

export type MarketIndexUpdate = MarketIndex;

export type MarketQuoteUpdate = {
  symbol: string;
  boardId: string;
  lastPrice: number | null;
  change: number | null;
  changePercent: number | null;
  lastQuantity: number | null;
  expectedPrice?: number | null;
  expectedQuantity?: number | null;
  totalVolume: number | null;
  totalValue: number | null;
  foreignBuyVolume: number | null;
  foreignSellVolume: number | null;
  foreignRoom: number | null;
  bidLevels: PriceLevel[] | null;
  askLevels: PriceLevel[] | null;
  tradingStatus: string | null;
  updatedAt: string;
  referencePrice?: number | null;
  ceilingPrice?: number | null;
  floorPrice?: number | null;
  openPrice?: number | null;
  highPrice?: number | null;
  lowPrice?: number | null;
};

export type QuoteStreamStatus = {
  provider: string;
  isEnabled: boolean;
  updatedAt: string;
  message: string;
};
