import type { MarketQuote, PriceLevel } from '../../shared/types/market';

export type PriceClass = 'ceiling' | 'floor' | 'reference' | 'up' | 'down' | 'neutral';

export type MarketBoardFlashField =
  | 'symbol'
  | 'ceilingPrice'
  | 'floorPrice'
  | 'referencePrice'
  | 'bid3Price'
  | 'bid3Quantity'
  | 'bid2Price'
  | 'bid2Quantity'
  | 'bid1Price'
  | 'bid1Quantity'
  | 'lastPrice'
  | 'lastQuantity'
  | 'change'
  | 'changePercent'
  | 'matchedPrice'
  | 'matchedQuantity'
  | 'matchedChange'
  | 'matchedChangePercent'
  | 'ask1Price'
  | 'ask1Quantity'
  | 'ask2Price'
  | 'ask2Quantity'
  | 'ask3Price'
  | 'ask3Quantity'
  | 'totalVolume'
  | 'foreignBuyVolume'
  | 'foreignSellVolume'
  | 'foreignRoom'
  | 'highPrice'
  | 'lowPrice';

export type MarketBoardFlashClasses = Partial<Record<MarketBoardFlashField, PriceClass>>;

export type MarketBoardRow = {
  id: string;
  symbol: string;
  displayName: string;
  boardId: string;
  marketId: string;
  ceilingPrice: number | null;
  floorPrice: number | null;
  referencePrice: number | null;
  bid3Price: number | null;
  bid3Quantity: number | null;
  bid2Price: number | null;
  bid2Quantity: number | null;
  bid1Price: number | null;
  bid1Quantity: number | null;
  lastPrice: number | null;
  lastQuantity: number | null;
  change: number | null;
  changePercent: number | null;
  matchedPrice: number | null;
  matchedQuantity: number | null;
  matchedChange: number | null;
  matchedChangePercent: number | null;
  ask1Price: number | null;
  ask1Quantity: number | null;
  ask2Price: number | null;
  ask2Quantity: number | null;
  ask3Price: number | null;
  ask3Quantity: number | null;
  totalVolume: number | null;
  foreignBuyVolume: number | null;
  foreignSellVolume: number | null;
  foreignRoom: number | null;
  highPrice: number | null;
  lowPrice: number | null;
  tradingStatus: string;
  updatedTime: string;
  symbolClass: PriceClass;
  ceilingPriceClass: PriceClass;
  floorPriceClass: PriceClass;
  referencePriceClass: PriceClass;
  bid3PriceClass: PriceClass;
  bid3QuantityClass: PriceClass;
  bid2PriceClass: PriceClass;
  bid2QuantityClass: PriceClass;
  bid1PriceClass: PriceClass;
  bid1QuantityClass: PriceClass;
  lastPriceClass: PriceClass;
  lastQuantityClass: PriceClass;
  changeClass: PriceClass;
  matchedPriceClass: PriceClass;
  matchedQuantityClass: PriceClass;
  matchedChangeClass: PriceClass;
  ask1PriceClass: PriceClass;
  ask1QuantityClass: PriceClass;
  ask2PriceClass: PriceClass;
  ask2QuantityClass: PriceClass;
  ask3PriceClass: PriceClass;
  ask3QuantityClass: PriceClass;
  highPriceClass: PriceClass;
  lowPriceClass: PriceClass;
  flashClasses: MarketBoardFlashClasses;
};

const numberFormatter = new Intl.NumberFormat('en-US');
const priceFormatter = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});
const percentFormatter = new Intl.NumberFormat('en-US', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
  signDisplay: 'exceptZero',
});
const timeFormatter = new Intl.DateTimeFormat('en-GB', {
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
  timeZone: 'Asia/Ho_Chi_Minh',
});

export function formatPrice(value: number | null | undefined) {
  if (value == null) {
    return '-';
  }

  const displayValue = Math.abs(value) >= 1000 ? value / 1000 : value;
  return priceFormatter.format(displayValue);
}

export function formatQuantity(value: number | null | undefined) {
  return value == null ? '-' : numberFormatter.format(value);
}

export function formatPercent(value: number | null | undefined) {
  return value == null ? '-' : `${percentFormatter.format(value)}%`;
}

export function formatChange(value: number | null | undefined, referencePrice?: number | null) {
  if (value == null) {
    return '-';
  }

  const displayValue = shouldScaleChange(value, referencePrice) ? value / 1000 : value;
  const formattedValue = priceFormatter.format(Math.abs(displayValue));

  if (value > 0) {
    return `+${formattedValue}`;
  }

  if (value < 0) {
    return `-${formattedValue}`;
  }

  return formattedValue;
}

function shouldScaleChange(value: number, referencePrice: number | null | undefined) {
  if (referencePrice != null && Math.abs(referencePrice) >= 1000) {
    return true;
  }

  return Math.abs(value) >= 100;
}

export function classifyChange(value: number | null | undefined): PriceClass {
  if (value == null) {
    return 'neutral';
  }

  if (value > 0) {
    return 'up';
  }

  if (value < 0) {
    return 'down';
  }

  return 'reference';
}

export function classifyPrice(value: number | null | undefined, quote: MarketQuote): PriceClass {
  if (value == null) {
    return 'neutral';
  }

  if (quote.ceilingPrice != null && value === quote.ceilingPrice) {
    return 'ceiling';
  }

  if (quote.floorPrice != null && value === quote.floorPrice) {
    return 'floor';
  }

  if (quote.referencePrice != null && value === quote.referencePrice) {
    return 'reference';
  }

  if (quote.referencePrice != null && value > quote.referencePrice) {
    return 'up';
  }

  if (quote.referencePrice != null && value < quote.referencePrice) {
    return 'down';
  }

  return 'neutral';
}

type MatchedDisplayValues = {
  matchedPrice: number | null;
  matchedQuantity: number | null;
  matchedChange: number | null;
  matchedChangePercent: number | null;
  matchedPriceClass: PriceClass;
  matchedQuantityClass: PriceClass;
  matchedChangeClass: PriceClass;
};

export function getMatchedDisplayValues(quote: MarketQuote): MatchedDisplayValues {
  const matchedPrice = quote.expectedPrice ?? quote.lastPrice;
  const matchedQuantity = quote.expectedQuantity ?? quote.lastQuantity;
  const matchedChange = resolveMatchedChange(quote, matchedPrice);
  const matchedChangePercent = resolveMatchedChangePercent(quote, matchedChange);
  const matchedPriceClass = classifyPrice(matchedPrice, quote);
  const matchedChangeClass = classifyChange(matchedChange);

  return {
    matchedPrice,
    matchedQuantity,
    matchedChange,
    matchedChangePercent,
    matchedPriceClass,
    matchedQuantityClass: matchedPriceClass,
    matchedChangeClass,
  };
}

export function mapQuoteToMarketBoardRow(quote: MarketQuote, flashClasses: MarketBoardFlashClasses = {}): MarketBoardRow {
  const bid1 = getLevel(quote.bidLevels, 0);
  const bid2 = getLevel(quote.bidLevels, 1);
  const bid3 = getLevel(quote.bidLevels, 2);
  const ask1 = getLevel(quote.askLevels, 0);
  const ask2 = getLevel(quote.askLevels, 1);
  const ask3 = getLevel(quote.askLevels, 2);
  const bid3Class = classifyPrice(bid3.price, quote);
  const bid2Class = classifyPrice(bid2.price, quote);
  const bid1Class = classifyPrice(bid1.price, quote);
  const lastClass = classifyPrice(quote.lastPrice, quote);
  const ask1Class = classifyPrice(ask1.price, quote);
  const ask2Class = classifyPrice(ask2.price, quote);
  const ask3Class = classifyPrice(ask3.price, quote);
  const matchedValues = getMatchedDisplayValues(quote);

  return {
    id: `${quote.boardId}:${quote.symbol}`,
    symbol: quote.symbol,
    displayName: quote.displayName,
    boardId: quote.boardId,
    marketId: quote.marketId,
    ceilingPrice: quote.ceilingPrice,
    floorPrice: quote.floorPrice,
    referencePrice: quote.referencePrice,
    bid3Price: bid3.price,
    bid3Quantity: bid3.quantity,
    bid2Price: bid2.price,
    bid2Quantity: bid2.quantity,
    bid1Price: bid1.price,
    bid1Quantity: bid1.quantity,
    lastPrice: quote.lastPrice,
    lastQuantity: quote.lastQuantity,
    change: quote.change,
    changePercent: quote.changePercent,
    matchedPrice: matchedValues.matchedPrice,
    matchedQuantity: matchedValues.matchedQuantity,
    matchedChange: matchedValues.matchedChange,
    matchedChangePercent: matchedValues.matchedChangePercent,
    ask1Price: ask1.price,
    ask1Quantity: ask1.quantity,
    ask2Price: ask2.price,
    ask2Quantity: ask2.quantity,
    ask3Price: ask3.price,
    ask3Quantity: ask3.quantity,
    totalVolume: quote.totalVolume,
    foreignBuyVolume: quote.foreignBuyVolume,
    foreignSellVolume: quote.foreignSellVolume,
    foreignRoom: quote.foreignRoom,
    highPrice: quote.highPrice,
    lowPrice: quote.lowPrice,
    tradingStatus: quote.tradingStatus,
    updatedTime: formatUpdatedTime(quote.updatedAt),
    symbolClass: matchedValues.matchedPriceClass,
    ceilingPriceClass: 'ceiling',
    floorPriceClass: 'floor',
    referencePriceClass: 'reference',
    bid3PriceClass: bid3Class,
    bid3QuantityClass: bid3Class,
    bid2PriceClass: bid2Class,
    bid2QuantityClass: bid2Class,
    bid1PriceClass: bid1Class,
    bid1QuantityClass: bid1Class,
    lastPriceClass: lastClass,
    lastQuantityClass: lastClass,
    changeClass: classifyChange(quote.change),
    matchedPriceClass: matchedValues.matchedPriceClass,
    matchedQuantityClass: matchedValues.matchedQuantityClass,
    matchedChangeClass: matchedValues.matchedChangeClass,
    ask1PriceClass: ask1Class,
    ask1QuantityClass: ask1Class,
    ask2PriceClass: ask2Class,
    ask2QuantityClass: ask2Class,
    ask3PriceClass: ask3Class,
    ask3QuantityClass: ask3Class,
    highPriceClass: classifyPrice(quote.highPrice, quote),
    lowPriceClass: classifyPrice(quote.lowPrice, quote),
    flashClasses,
  };
}

function getLevel(levels: PriceLevel[], index: number): PriceLevel {
  return levels[index] ?? { price: null, quantity: null };
}

function resolveMatchedChange(quote: MarketQuote, matchedPrice: number | null) {
  if (quote.expectedPrice == null) {
    return quote.change;
  }

  return calculateChange(matchedPrice, quote.referencePrice) ?? quote.change;
}

function resolveMatchedChangePercent(quote: MarketQuote, matchedChange: number | null) {
  if (quote.expectedPrice == null) {
    return quote.changePercent;
  }

  if (matchedChange == null || quote.referencePrice == null || quote.referencePrice <= 0) {
    return quote.changePercent;
  }

  return roundPercent((matchedChange / quote.referencePrice) * 100);
}

function calculateChange(price: number | null, referencePrice: number | null) {
  if (price == null || referencePrice == null || referencePrice <= 0) {
    return null;
  }

  return roundPriceDelta(price - referencePrice);
}

function roundPriceDelta(value: number) {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

function roundPercent(value: number) {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

function formatUpdatedTime(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return '-';
  }

  return timeFormatter.format(date);
}
