import type { MarketQuote, MarketQuoteUpdate, PriceLevel } from '../../shared/types/market';
import {
  classifyChange,
  classifyPrice,
  formatChange,
  formatPercent,
  formatPrice,
  formatQuantity,
  getMatchedDisplayValues,
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

    const sourceQuote = normalizeQuotePriceScaleForUpdate(quote, update);
    previousQuote = sourceQuote;
    const nextReferencePrice = keepExistingWhenNullish(update.referencePrice, sourceQuote.referencePrice);
    const nextLastPrice = keepExistingWhenNullish(update.lastPrice, sourceQuote.lastPrice);
    const nextChange = resolveChange(update, sourceQuote, nextLastPrice, nextReferencePrice);
    const nextChangePercent = resolveChangePercent(update, sourceQuote, nextChange, nextReferencePrice);
    const shouldClearExpectedAuction =
      hasActualMatchedUpdate(update) && update.expectedPrice == null && update.expectedQuantity == null;

    updatedQuote = {
      ...sourceQuote,
      referencePrice: nextReferencePrice,
      ceilingPrice: keepExistingWhenNullish(update.ceilingPrice, sourceQuote.ceilingPrice),
      floorPrice: keepExistingWhenNullish(update.floorPrice, sourceQuote.floorPrice),
      lastPrice: nextLastPrice,
      change: nextChange,
      changePercent: nextChangePercent,
      lastQuantity: keepExistingWhenNullish(update.lastQuantity, sourceQuote.lastQuantity),
      expectedPrice: shouldClearExpectedAuction
        ? null
        : keepExistingWhenNullish(update.expectedPrice, sourceQuote.expectedPrice ?? null),
      expectedQuantity: shouldClearExpectedAuction
        ? null
        : keepExistingWhenNullish(update.expectedQuantity, sourceQuote.expectedQuantity ?? null),
      totalVolume: keepExistingWhenNullish(update.totalVolume, sourceQuote.totalVolume),
      totalValue: keepExistingWhenNullish(update.totalValue, sourceQuote.totalValue),
      foreignBuyVolume: keepExistingWhenNullish(update.foreignBuyVolume, sourceQuote.foreignBuyVolume),
      foreignSellVolume: keepExistingWhenNullish(update.foreignSellVolume, sourceQuote.foreignSellVolume),
      foreignRoom: keepExistingWhenNullish(update.foreignRoom, sourceQuote.foreignRoom),
      openPrice: keepExistingWhenNullish(update.openPrice, sourceQuote.openPrice),
      highPrice: keepExistingWhenNullish(update.highPrice, sourceQuote.highPrice),
      lowPrice: keepExistingWhenNullish(update.lowPrice, sourceQuote.lowPrice),
      bidLevels: keepLevelsWhenNullish(update.bidLevels, sourceQuote.bidLevels),
      askLevels: keepLevelsWhenNullish(update.askLevels, sourceQuote.askLevels),
      tradingStatus: keepExistingWhenNullish(update.tradingStatus, sourceQuote.tradingStatus),
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
  const previousMatchedValues = getMatchedDisplayValues(previousQuote);
  const updatedMatchedValues = getMatchedDisplayValues(updatedQuote);

  if (hasDisplayedValueChanged(previousQuote.ceilingPrice, updatedQuote.ceilingPrice, update.ceilingPrice, formatPrice)) {
    setFlash(flashClasses, 'ceilingPrice', 'ceiling');
  }

  if (hasDisplayedValueChanged(previousQuote.floorPrice, updatedQuote.floorPrice, update.floorPrice, formatPrice)) {
    setFlash(flashClasses, 'floorPrice', 'floor');
  }

  if (hasDisplayedValueChanged(previousQuote.referencePrice, updatedQuote.referencePrice, update.referencePrice, formatPrice)) {
    setFlash(flashClasses, 'referencePrice', 'reference');
  }

  if (hasDisplayedValueChanged(previousQuote.lastPrice, updatedQuote.lastPrice, update.lastPrice, formatPrice)) {
    setFlash(flashClasses, 'lastPrice', lastPriceClass);
  }

  if (hasDisplayedValueChanged(previousQuote.lastQuantity, updatedQuote.lastQuantity, update.lastQuantity, formatQuantity)) {
    setFlash(flashClasses, 'lastQuantity', lastPriceClass);
  }

  if (hasDerivedDisplayedValueChanged(previousQuote.change, updatedQuote.change, update.change, update.lastPrice, formatChange)) {
    setFlash(flashClasses, 'change', changeClass);
  }

  if (
    hasDerivedDisplayedValueChanged(
      previousQuote.changePercent,
      updatedQuote.changePercent,
      update.changePercent,
      update.lastPrice,
      formatPercent,
    )
  ) {
    setFlash(flashClasses, 'changePercent', changeClass);
  }

  if (
    hasDisplayedValueChanged(
      previousMatchedValues.matchedPrice,
      updatedMatchedValues.matchedPrice,
      update.expectedPrice ?? update.lastPrice,
      formatPrice,
    )
  ) {
    setFlash(flashClasses, 'matchedPrice', updatedMatchedValues.matchedPriceClass);
  }

  if (
    hasDisplayedValueChanged(
      previousMatchedValues.matchedQuantity,
      updatedMatchedValues.matchedQuantity,
      update.expectedQuantity ?? update.lastQuantity,
      formatQuantity,
    )
  ) {
    setFlash(flashClasses, 'matchedQuantity', updatedMatchedValues.matchedPriceClass);
  }

  if (
    hasDerivedDisplayedValueChanged(
      previousMatchedValues.matchedChange,
      updatedMatchedValues.matchedChange,
      update.change ?? update.expectedPrice,
      update.lastPrice,
      formatChange,
    )
  ) {
    setFlash(flashClasses, 'matchedChange', updatedMatchedValues.matchedChangeClass);
  }

  if (
    hasDerivedDisplayedValueChanged(
      previousMatchedValues.matchedChangePercent,
      updatedMatchedValues.matchedChangePercent,
      update.changePercent ?? update.expectedPrice,
      update.lastPrice,
      formatPercent,
    )
  ) {
    setFlash(flashClasses, 'matchedChangePercent', updatedMatchedValues.matchedChangeClass);
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

  if (hasDisplayedValueChanged(previousQuote.highPrice, updatedQuote.highPrice, update.highPrice, formatPrice)) {
    setFlash(flashClasses, 'highPrice', classifyPrice(updatedQuote.highPrice, updatedQuote));
  }

  if (hasDisplayedValueChanged(previousQuote.lowPrice, updatedQuote.lowPrice, update.lowPrice, formatPrice)) {
    setFlash(flashClasses, 'lowPrice', classifyPrice(updatedQuote.lowPrice, updatedQuote));
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

function hasActualMatchedUpdate(update: MarketQuoteUpdate) {
  return update.lastPrice != null || update.lastQuantity != null;
}

function normalizeQuotePriceScaleForUpdate(quote: MarketQuote, update: MarketQuoteUpdate): MarketQuote {
  if (!hasScaledPrice(update) || !hasUnscaledQuotePrice(quote)) {
    return quote;
  }

  return {
    ...quote,
    referencePrice: scalePrice(quote.referencePrice),
    ceilingPrice: scalePrice(quote.ceilingPrice),
    floorPrice: scalePrice(quote.floorPrice),
    lastPrice: scalePrice(quote.lastPrice),
    change: scaleChange(quote.change),
    expectedPrice: scalePrice(quote.expectedPrice ?? null),
    openPrice: scalePrice(quote.openPrice),
    highPrice: scalePrice(quote.highPrice),
    lowPrice: scalePrice(quote.lowPrice),
    bidLevels: scalePriceLevels(quote.bidLevels),
    askLevels: scalePriceLevels(quote.askLevels),
  };
}

function hasScaledPrice(update: MarketQuoteUpdate) {
  return [
    update.referencePrice,
    update.ceilingPrice,
    update.floorPrice,
    update.lastPrice,
    update.expectedPrice,
    update.openPrice,
    update.highPrice,
    update.lowPrice,
    ...collectLevelPrices(update.bidLevels),
    ...collectLevelPrices(update.askLevels),
  ].some(isScaledPrice);
}

function hasUnscaledQuotePrice(quote: MarketQuote) {
  return [
    quote.referencePrice,
    quote.ceilingPrice,
    quote.floorPrice,
    quote.lastPrice,
    quote.expectedPrice,
    quote.openPrice,
    quote.highPrice,
    quote.lowPrice,
    ...collectLevelPrices(quote.bidLevels),
    ...collectLevelPrices(quote.askLevels),
  ].some(isUnscaledPrice);
}

function collectLevelPrices(levels: PriceLevel[] | null | undefined) {
  return levels?.map((level) => level.price) ?? [];
}

function scalePriceLevels(levels: PriceLevel[]) {
  return levels.map((level) => ({ ...level, price: scalePrice(level.price) }));
}

function scalePrice(value: number | null) {
  return isUnscaledPrice(value) ? roundPriceDelta(value * 1000) : value;
}

function scaleChange(value: number | null) {
  return value != null && value !== 0 && Math.abs(value) < 100 ? roundPriceDelta(value * 1000) : value;
}

function isScaledPrice(value: number | null | undefined): value is number {
  return value != null && Math.abs(value) >= 1000;
}

function isUnscaledPrice(value: number | null | undefined): value is number {
  return value != null && value > 0 && Math.abs(value) < 1000;
}

function resolveChange(
  update: MarketQuoteUpdate,
  quote: MarketQuote,
  nextLastPrice: number | null,
  nextReferencePrice: number | null,
) {
  if (update.change != null) {
    return update.change;
  }

  if (update.lastPrice != null && nextLastPrice != null && nextReferencePrice != null && nextReferencePrice > 0) {
    return roundPriceDelta(nextLastPrice - nextReferencePrice);
  }

  return quote.change;
}

function resolveChangePercent(
  update: MarketQuoteUpdate,
  quote: MarketQuote,
  nextChange: number | null,
  nextReferencePrice: number | null,
) {
  if (update.changePercent != null) {
    return update.changePercent;
  }

  if (update.lastPrice != null && nextChange != null && nextReferencePrice != null && nextReferencePrice > 0) {
    return roundPercent((nextChange / nextReferencePrice) * 100);
  }

  return quote.changePercent;
}

function roundPriceDelta(value: number) {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

function roundPercent(value: number) {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

function hasDisplayedValueChanged<T>(
  previousValue: T,
  updatedValue: T,
  updateValue: T | null | undefined,
  formatter: (value: T) => string,
) {
  return updateValue != null && isDisplayedValueDifferent(previousValue, updatedValue, formatter);
}

function hasDerivedDisplayedValueChanged<T>(
  previousValue: T,
  updatedValue: T,
  updateValue: T | null | undefined,
  triggerValue: unknown,
  formatter: (value: T) => string,
) {
  return (updateValue != null || triggerValue != null) && isDisplayedValueDifferent(previousValue, updatedValue, formatter);
}

function isDisplayedValueDifferent<T>(previousValue: T, updatedValue: T, formatter: (value: T) => string) {
  return formatter(previousValue) !== formatter(updatedValue);
}

function getLevel(levels: PriceLevel[], index: number): PriceLevel {
  return levels[index] ?? { price: null, quantity: null };
}
