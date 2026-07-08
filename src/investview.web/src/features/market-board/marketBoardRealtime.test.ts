import { describe, expect, it } from 'vitest';
import { applyQuoteUpdate } from './marketBoardRealtime';
import { formatChange, formatPercent } from './marketBoardFormatters';
import type { MarketQuote, MarketQuoteUpdate } from '../../shared/types/market';

const baseQuote: MarketQuote = {
  symbol: 'HPG',
  boardId: 'G1',
  marketId: 'HOSE',
  displayName: 'Hoa Phat Group',
  referencePrice: 27.4,
  ceilingPrice: 29.3,
  floorPrice: 25.5,
  lastPrice: 28.1,
  change: 0.7,
  changePercent: 2.55,
  lastQuantity: 18_000,
  totalVolume: 12_345_678,
  totalValue: 347_000_000_000,
  foreignBuyVolume: 786_100,
  foreignSellVolume: 1_227_649,
  foreignRoom: 1_742_502_798,
  openPrice: 27.6,
  highPrice: 28.4,
  lowPrice: 27.2,
  bidLevels: [
    { price: 28, quantity: 45_000 },
    { price: 27.9, quantity: 31_000 },
    { price: 27.8, quantity: 16_000 },
  ],
  askLevels: [
    { price: 28.1, quantity: 21_000 },
    { price: 28.2, quantity: 24_000 },
    { price: 28.3, quantity: 33_000 },
  ],
  tradingStatus: 'Continuous',
  updatedAt: '2026-07-03T07:45:00Z',
};

const update: MarketQuoteUpdate = {
  symbol: 'hpg',
  boardId: 'g1',
  lastPrice: 28.35,
  change: 0.95,
  changePercent: 3.47,
  lastQuantity: 20_000,
  totalVolume: 12_365_678,
  totalValue: 348_000_000_000,
  foreignBuyVolume: 800_100,
  foreignSellVolume: 1_230_649,
  foreignRoom: 1_742_488_798,
  bidLevels: [{ price: 28.3, quantity: 50_000 }],
  askLevels: [{ price: 28.4, quantity: 22_000 }],
  tradingStatus: 'Continuous',
  updatedAt: '2026-07-03T07:45:03Z',
};

describe('applyQuoteUpdate', () => {
  it('updates only the quote matching symbol and board id', () => {
    const ssiQuote = { ...baseQuote, symbol: 'SSI', boardId: 'G1', lastPrice: 33.5 };

    const result = applyQuoteUpdate([baseQuote, ssiQuote], update);

    expect(result.updatedQuote).toMatchObject({
      symbol: 'HPG',
      boardId: 'G1',
      lastPrice: 28.35,
      totalVolume: 12_365_678,
      foreignRoom: 1_742_488_798,
      updatedAt: '2026-07-03T07:45:03Z',
    });
    expect(result.flashClasses).toMatchObject({
      lastPrice: 'up',
      lastQuantity: 'up',
      change: 'up',
      changePercent: 'up',
      totalVolume: 'neutral',
      foreignBuyVolume: 'neutral',
      foreignSellVolume: 'neutral',
      foreignRoom: 'neutral',
      bid1Price: 'up',
      bid1Quantity: 'up',
      ask1Price: 'up',
      ask1Quantity: 'up',
    });
    expect(result.quotes[1]).toBe(ssiQuote);
  });

  it('preserves existing fields when a partial update sends null values', () => {
    const result = applyQuoteUpdate([baseQuote], {
      ...update,
      lastPrice: null,
      totalVolume: null,
      bidLevels: null,
      askLevels: null,
      tradingStatus: null,
    });

    expect(result.updatedQuote).toMatchObject({
      lastPrice: 28.1,
      totalVolume: 12_345_678,
      tradingStatus: 'Continuous',
      updatedAt: '2026-07-03T07:45:03Z',
    });
    expect(result.updatedQuote?.bidLevels).toBe(baseQuote.bidLevels);
    expect(result.updatedQuote?.askLevels).toBe(baseQuote.askLevels);
  });

  it('applies security definition fields from realtime updates', () => {
    const result = applyQuoteUpdate([baseQuote], {
      ...update,
      referencePrice: 27.5,
      ceilingPrice: 29.4,
      floorPrice: 25.6,
      openPrice: null,
      highPrice: null,
      lowPrice: null,
      lastPrice: null,
      change: null,
      changePercent: null,
      lastQuantity: null,
      totalVolume: null,
      totalValue: null,
      foreignBuyVolume: null,
      foreignSellVolume: null,
      foreignRoom: null,
      bidLevels: null,
      askLevels: null,
      tradingStatus: 'NO_HALT',
    });

    expect(result.updatedQuote).toMatchObject({
      referencePrice: 27.5,
      ceilingPrice: 29.4,
      floorPrice: 25.6,
      tradingStatus: 'NO_HALT',
    });
    expect(result.flashClasses).toMatchObject({
      referencePrice: 'reference',
      ceilingPrice: 'ceiling',
      floorPrice: 'floor',
    });
  });

  it('uses reference comparison for flash classes instead of old-versus-new direction', () => {
    const result = applyQuoteUpdate([baseQuote], {
      ...update,
      lastPrice: baseQuote.referencePrice,
      change: 0,
      changePercent: 0,
      bidLevels: [{ price: baseQuote.floorPrice, quantity: 10_000 }],
      askLevels: [{ price: baseQuote.ceilingPrice, quantity: 12_000 }],
    });

    expect(result.flashClasses).toMatchObject({
      lastPrice: 'reference',
      change: 'reference',
      changePercent: 'reference',
      bid1Price: 'floor',
      bid1Quantity: 'floor',
      ask1Price: 'ceiling',
      ask1Quantity: 'ceiling',
    });
  });

  it('computes change and percent from snapshot reference when realtime trade omits them', () => {
    const result = applyQuoteUpdate([baseQuote], {
      ...update,
      lastPrice: 28.35,
      change: null,
      changePercent: null,
      lastQuantity: 20_000,
      totalVolume: null,
      totalValue: null,
      foreignBuyVolume: null,
      foreignSellVolume: null,
      foreignRoom: null,
      bidLevels: null,
      askLevels: null,
      tradingStatus: null,
    });

    expect(result.updatedQuote).toMatchObject({
      lastPrice: 28.35,
      change: 0.95,
      changePercent: 3.47,
    });
    expect(result.flashClasses).toMatchObject({
      lastPrice: 'up',
      change: 'up',
      changePercent: 'up',
    });
  });

  it('computes scaled realtime change values from snapshot reference', () => {
    const scaledQuote: MarketQuote = {
      ...baseQuote,
      referencePrice: 27_400,
      lastPrice: 28_100,
      change: 700,
      changePercent: 2.55,
    };

    const result = applyQuoteUpdate([scaledQuote], {
      ...update,
      lastPrice: 28_350,
      change: null,
      changePercent: null,
      lastQuantity: null,
      totalVolume: null,
      totalValue: null,
      foreignBuyVolume: null,
      foreignSellVolume: null,
      foreignRoom: null,
      bidLevels: null,
      askLevels: null,
      tradingStatus: null,
    });

    expect(result.updatedQuote).toMatchObject({
      lastPrice: 28_350,
      change: 950,
      changePercent: 3.47,
    });
  });

  it('normalizes mixed snapshot and realtime price units before computing change values', () => {
    const vcbQuote: MarketQuote = {
      ...baseQuote,
      symbol: 'VCB',
      referencePrice: 61.3,
      ceilingPrice: 65.5,
      floorPrice: 57.1,
      lastPrice: 61.3,
      change: 0,
      changePercent: 0,
      bidLevels: [
        { price: 61.4, quantity: 83_700 },
        { price: 61.3, quantity: 87_700 },
      ],
      askLevels: [{ price: 61.6, quantity: 34_300 }],
    };

    const result = applyQuoteUpdate([vcbQuote], {
      ...update,
      symbol: 'VCB',
      lastPrice: 61_500,
      change: null,
      changePercent: null,
      lastQuantity: 200,
      totalVolume: null,
      totalValue: null,
      foreignBuyVolume: null,
      foreignSellVolume: null,
      foreignRoom: null,
      bidLevels: null,
      askLevels: null,
      tradingStatus: null,
    });

    expect(result.updatedQuote).toMatchObject({
      referencePrice: 61_300,
      ceilingPrice: 65_500,
      floorPrice: 57_100,
      lastPrice: 61_500,
      change: 200,
      changePercent: 0.33,
    });
    expect(formatChange(result.updatedQuote?.change)).toBe('+0.20');
    expect(formatPercent(result.updatedQuote?.changePercent)).toBe('+0.33%');
    expect(result.flashClasses).toMatchObject({
      lastPrice: 'up',
      change: 'up',
      changePercent: 'up',
    });
  });

  it('uses the changed cell semantics for flash color instead of the row last price', () => {
    const result = applyQuoteUpdate([baseQuote], {
      ...update,
      lastPrice: 28.3,
      change: -0.1,
      changePercent: -0.36,
      totalVolume: baseQuote.totalVolume! + 1_000,
      foreignBuyVolume: baseQuote.foreignBuyVolume! + 1_000,
      foreignSellVolume: baseQuote.foreignSellVolume! + 1_000,
      foreignRoom: baseQuote.foreignRoom! - 1_000,
      bidLevels: null,
      askLevels: null,
    });

    expect(result.flashClasses).toMatchObject({
      lastPrice: 'up',
      lastQuantity: 'up',
      change: 'down',
      changePercent: 'down',
      totalVolume: 'neutral',
      foreignBuyVolume: 'neutral',
      foreignSellVolume: 'neutral',
      foreignRoom: 'neutral',
    });
    expect(result.flashClasses).not.toHaveProperty('symbol');
  });

  it('flashes only cells whose displayed values changed', () => {
    const result = applyQuoteUpdate([baseQuote], {
      ...update,
      lastPrice: baseQuote.lastPrice,
      change: baseQuote.change,
      changePercent: baseQuote.changePercent,
      lastQuantity: baseQuote.lastQuantity,
      totalVolume: baseQuote.totalVolume,
      totalValue: baseQuote.totalValue,
      foreignBuyVolume: baseQuote.foreignBuyVolume,
      foreignSellVolume: baseQuote.foreignSellVolume,
      foreignRoom: baseQuote.foreignRoom,
      bidLevels: [{ price: baseQuote.bidLevels[0]!.price, quantity: baseQuote.bidLevels[0]!.quantity! + 1_000 }],
      askLevels: [{ price: baseQuote.askLevels[0]!.price, quantity: baseQuote.askLevels[0]!.quantity }],
    });

    expect(result.flashClasses).toEqual({
      bid1Quantity: 'up',
    });
  });

  it('does not flash a cell when the formatted display value is unchanged', () => {
    const quoteWithScaledPrice = {
      ...baseQuote,
      lastPrice: 28_100,
      change: 700,
      changePercent: 2.55,
      totalVolume: 12_345_678,
      bidLevels: [{ price: 28_000, quantity: 45_000 }],
      askLevels: [{ price: 28_100, quantity: 21_000 }],
    };

    const result = applyQuoteUpdate([quoteWithScaledPrice], {
      ...update,
      lastPrice: 28.1,
      change: 0.7,
      changePercent: 2.55,
      totalVolume: 12_345_678,
      foreignBuyVolume: baseQuote.foreignBuyVolume,
      foreignSellVolume: baseQuote.foreignSellVolume,
      foreignRoom: baseQuote.foreignRoom,
      bidLevels: [{ price: 28, quantity: 45_000 }],
      askLevels: [{ price: 28.1, quantity: 21_000 }],
    });

    expect(result.flashClasses).toEqual({
      lastQuantity: 'up',
    });
  });

  it('returns the original quote array when no row matches the update', () => {
    const quotes = [baseQuote];

    const result = applyQuoteUpdate(quotes, { ...update, symbol: 'VCB' });

    expect(result.quotes).toBe(quotes);
    expect(result.quotes[0]).toBe(baseQuote);
    expect(result.updatedQuote).toBeNull();
  });
});
