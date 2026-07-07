import type { MarketQuote, MarketQuoteUpdate, PriceLevel } from '../../shared/types/market';
import {
  classifyChange,
  classifyPrice,
  formatChange,
  formatPercent,
  formatPrice,
  formatQuantity,
  type MarketBoardFlashClasses,
  type MarketBoardFlashField,
  type PriceClass,
} from './marketBoardFormatters';

export type QuoteUpdateResult = {
  quotes: MarketQuote[];
  updatedQuote: MarketQuote | null;
  flashClasses: MarketBoardFlashClasses;
};

export function applyQuoteUpdate(quotes: MarketQuote[], update: MarketQuoteUpdate): QuoteUpdateResult {
  const targetSymbol = normalizeKey(update.symbol);
  const targetBoardId = normalizeKey(update.boardId);
  let previousQuote: MarketQuote | null = null;
  let updatedQuote: MarketQuote | null = null;

  const nextQuotes = quotes.map((quote) => {
    if (normalizeKey(quote.symbol) !== targetSymbol || normalizeKey(quote.boardId) !== targetBoardId) {
      return quote;
    }

    previousQuote = quote;
    updatedQuote = {
      ...quote,
      lastPrice: keepExistingWhenNullish(update.lastPrice, quote.lastPrice),
      change: keepExistingWhenNullish(update.change, quote.change),
      changePercent: keepExistingWhenNullish(update.changePercent, quote.changePercent),
      lastQuantity: keepExistingWhenNullish(update.lastQuantity, quote.lastQuantity),
      totalVolume: keepExistingWhenNullish(update.totalVolume, quote.totalVolume),
      totalValue: keepExistingWhenNullish(update.totalValue, quote.totalValue),
      foreignBuyVolume: keepExistingWhenNullish(update.foreignBuyVolume, quote.foreignBuyVolume),
      foreignSellVolume: keepExistingWhenNullish(update.foreignSellVolume, quote.foreignSellVolume),
      foreignRoom: keepExistingWhenNullish(update.foreignRoom, quote.foreignRoom),
      bidLevels: keepLevelsWhenNullish(update.bidLevels, quote.bidLevels),
      askLevels: keepLevelsWhenNullish(update.askLevels, quote.askLevels),
      tradingStatus: keepExistingWhenNullish(update.tradingStatus, quote.tradingStatus),
      updatedAt: update.updatedAt,
    };

    return updatedQuote;
  });

  if (updatedQuote == null || previousQuote == null) {
    return { quotes, updatedQuote: null, flashClasses: {} };
  }

  return {
    quotes: nextQuotes,
    updatedQuote,
    flashClasses: createFlashClasses(previousQuote, updatedQuote, update),
  };
}

function createFlashClasses(
  previousQuote: MarketQuote,
  updatedQuote: MarketQuote,
  update: MarketQuoteUpdate,
): MarketBoardFlashClasses {
  const flashClasses: MarketBoardFlashClasses = {};
  const lastPriceClass = classifyPrice(updatedQuote.lastPrice, updatedQuote);
  const changeClass = classifyChange(updatedQuote.change);

  if (hasDisplayedValueChanged(previousQuote.lastPrice, updatedQuote.lastPrice, update.lastPrice, formatPrice)) {
    setFlash(flashClasses, 'lastPrice', lastPriceClass);
  }

  if (hasDisplayedValueChanged(previousQuote.lastQuantity, updatedQuote.lastQuantity, update.lastQuantity, formatQuantity)) {
    setFlash(flashClasses, 'lastQuantity', lastPriceClass);
  }

  if (hasDisplayedValueChanged(previousQuote.change, updatedQuote.change, update.change, formatChange)) {
    setFlash(flashClasses, 'change', changeClass);
  }

  if (hasDisplayedValueChanged(previousQuote.changePercent, updatedQuote.changePercent, update.changePercent, formatPercent)) {
    setFlash(flashClasses, 'changePercent', changeClass);
  }

  if (hasDisplayedValueChanged(previousQuote.totalVolume, updatedQuote.totalVolume, update.totalVolume, formatQuantity)) {
    setFlash(flashClasses, 'totalVolume', 'neutral');
  }

  if (hasDisplayedValueChanged(previousQuote.foreignBuyVolume, updatedQuote.foreignBuyVolume, update.foreignBuyVolume, formatQuantity)) {
    setFlash(flashClasses, 'foreignBuyVolume', 'neutral');
  }

  if (hasDisplayedValueChanged(previousQuote.foreignSellVolume, updatedQuote.foreignSellVolume, update.foreignSellVolume, formatQuantity)) {
    setFlash(flashClasses, 'foreignSellVolume', 'neutral');
  }

  if (hasDisplayedValueChanged(previousQuote.foreignRoom, updatedQuote.foreignRoom, update.foreignRoom, formatQuantity)) {
    setFlash(flashClasses, 'foreignRoom', 'neutral');
  }

  addLevelFlashClasses(flashClasses, 'bid', previousQuote.bidLevels, updatedQuote.bidLevels, update.bidLevels, updatedQuote);
  addLevelFlashClasses(flashClasses, 'ask', previousQuote.askLevels, updatedQuote.askLevels, update.askLevels, updatedQuote);

  return flashClasses;
}

function addLevelFlashClasses(
  flashClasses: MarketBoardFlashClasses,
  side: 'bid' | 'ask',
  previousLevels: PriceLevel[],
  updatedLevels: PriceLevel[],
  updateLevels: PriceLevel[] | null,
  quote: MarketQuote,
) {
  if (updateLevels == null) {
    return;
  }

  updateLevels.slice(0, 3).forEach((_, index) => {
    const previousLevel = getLevel(previousLevels, index);
    const updatedLevel = getLevel(updatedLevels, index);
    const priceClass = classifyPrice(updatedLevel.price, quote);
    const levelNumber = index + 1;
    const prefix = `${side}${levelNumber}` as const;

    if (isDisplayedValueDifferent(previousLevel.price, updatedLevel.price, formatPrice)) {
      setFlash(flashClasses, `${prefix}Price` as MarketBoardFlashField, priceClass);
    }

    if (isDisplayedValueDifferent(previousLevel.quantity, updatedLevel.quantity, formatQuantity)) {
      setFlash(flashClasses, `${prefix}Quantity` as MarketBoardFlashField, priceClass);
    }
  });
}

function setFlash(flashClasses: MarketBoardFlashClasses, field: MarketBoardFlashField, priceClass: PriceClass) {
  flashClasses[field] = priceClass;
}

function normalizeKey(value: string) {
  return value.trim().toUpperCase();
}

function keepExistingWhenNullish<T>(nextValue: T | null | undefined, currentValue: T) {
  return nextValue == null ? currentValue : nextValue;
}

function keepLevelsWhenNullish(nextLevels: PriceLevel[] | null | undefined, currentLevels: PriceLevel[]) {
  return nextLevels == null ? currentLevels : nextLevels;
}

function hasDisplayedValueChanged<T>(
  previousValue: T,
  updatedValue: T,
  updateValue: T | null | undefined,
  formatter: (value: T) => string,
) {
  return updateValue != null && isDisplayedValueDifferent(previousValue, updatedValue, formatter);
}

function isDisplayedValueDifferent<T>(previousValue: T, updatedValue: T, formatter: (value: T) => string) {
  return formatter(previousValue) !== formatter(updatedValue);
}

function getLevel(levels: PriceLevel[], index: number): PriceLevel {
  return levels[index] ?? { price: null, quantity: null };
}
