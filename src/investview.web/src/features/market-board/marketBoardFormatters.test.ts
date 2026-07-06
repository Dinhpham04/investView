import { describe, expect, it } from 'vitest';
import {
  classifyChange,
  classifyPrice,
  formatPercent,
  formatPrice,
  formatQuantity,
  mapQuoteToMarketBoardRow,
} from './marketBoardFormatters';
import type { MarketQuote } from '../../shared/types/market';

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

describe('market board formatting', () => {
  it('formats prices, quantities, and percents for dense board cells', () => {
    expect(formatPrice(28.123)).toBe('28.12');
    expect(formatPrice(29_150)).toBe('29.15');
    expect(formatPrice(null)).toBe('-');
    expect(formatQuantity(12_345_678)).toBe('12,345,678');
    expect(formatQuantity(null)).toBe('-');
    expect(formatPercent(2.55)).toBe('+2.55%');
    expect(formatPercent(-1.2)).toBe('-1.20%');
    expect(formatPercent(0)).toBe('0.00%');
  });

  it('classifies prices using Vietnamese market color precedence', () => {
    expect(classifyPrice(29.3, baseQuote)).toBe('ceiling');
    expect(classifyPrice(25.5, baseQuote)).toBe('floor');
    expect(classifyPrice(27.4, baseQuote)).toBe('reference');
    expect(classifyPrice(28.1, baseQuote)).toBe('up');
    expect(classifyPrice(27.2, baseQuote)).toBe('down');
    expect(classifyPrice(null, baseQuote)).toBe('neutral');
  });

  it('classifies signed change values', () => {
    expect(classifyChange(0.7)).toBe('up');
    expect(classifyChange(-0.2)).toBe('down');
    expect(classifyChange(0)).toBe('reference');
    expect(classifyChange(null)).toBe('neutral');
  });

  it('maps quotes into stable market board rows with three bid and ask levels', () => {
    const row = mapQuoteToMarketBoardRow(baseQuote);

    expect(row.id).toBe('G1:HPG');
    expect(row.symbol).toBe('HPG');
    expect(row.bid3Price).toBe(27.8);
    expect(row.bid1Quantity).toBe(45_000);
    expect(row.ask1Price).toBe(28.1);
    expect(row.ask3Quantity).toBe(33_000);
    expect(row.lastPriceClass).toBe('up');
    expect(row.updatedTime).toBe('14:45:00');
  });
});
