import type { CellClassParams, CellClassRules, ColDef, ColGroupDef } from 'ag-grid-community';
import { describe, expect, it } from 'vitest';
import { marketBoardColumnDefs } from './marketBoardColumns';
import { mapQuoteToMarketBoardRow, type MarketBoardRow } from './marketBoardFormatters';
import type { MarketQuote } from '../../shared/types/market';

const quote: MarketQuote = {
  symbol: 'HPG',
  boardId: 'G1',
  marketId: 'HOSE',
  displayName: 'Hoa Phat Group',
  referencePrice: 27.4,
  ceilingPrice: 29.3,
  floorPrice: 25.5,
  lastPrice: 28.35,
  change: 0.95,
  changePercent: 3.47,
  lastQuantity: 20_000,
  totalVolume: 12_365_678,
  totalValue: 348_000_000_000,
  foreignBuyVolume: 800_100,
  foreignSellVolume: 1_230_649,
  foreignRoom: 1_742_488_798,
  openPrice: 27.6,
  highPrice: 28.4,
  lowPrice: 27.2,
  bidLevels: [{ price: 28.3, quantity: 50_000 }],
  askLevels: [{ price: 28.4, quantity: 22_000 }],
  tradingStatus: 'Continuous',
  updatedAt: '2026-07-03T07:45:03Z',
};

describe('marketBoardColumnDefs', () => {
  it('keeps the base price color class separate from removable flash rules', () => {
    const row = mapQuoteToMarketBoardRow(quote, { matchedPrice: 'up' });
    const matchedPriceColumn = findColumnByField('matchedPrice');

    expect(matchedPriceColumn.cellClass).toEqual(expect.arrayContaining(['market-cell', 'market-cell--number', 'ag-cell-bg-highlight']));
    expect(matchedPriceColumn.cellClass).not.toContain('quote-cell-flash');
    expect(ruleApplies(matchedPriceColumn.cellClassRules, 'quote-price-up', row)).toBe(true);
    expect(ruleApplies(matchedPriceColumn.cellClassRules, 'quote-cell-flash', row)).toBe(true);
    expect(ruleApplies(matchedPriceColumn.cellClassRules, 'quote-flash-up', row)).toBe(true);
  });

  it('turns flash classes off after the row flash marker is cleared', () => {
    const row = mapQuoteToMarketBoardRow(quote);
    const matchedPriceColumn = findColumnByField('matchedPrice');

    expect(ruleApplies(matchedPriceColumn.cellClassRules, 'quote-cell-flash', row)).toBe(false);
    expect(ruleApplies(matchedPriceColumn.cellClassRules, 'quote-flash-up', row)).toBe(false);
  });
});

function findColumnByField(field: keyof MarketBoardRow): ColDef<MarketBoardRow> {
  const columns = flattenColumns(marketBoardColumnDefs);
  const column = columns.find((item) => item.field === field);

  if (column == null) {
    throw new Error(`Missing market board column for ${String(field)}`);
  }

  return column;
}

function flattenColumns(columns: (ColDef<MarketBoardRow> | ColGroupDef<MarketBoardRow>)[]): ColDef<MarketBoardRow>[] {
  return columns.flatMap((column) => ('children' in column ? flattenColumns(column.children ?? []) : [column]));
}

function ruleApplies(
  rules: CellClassRules<MarketBoardRow> | undefined,
  className: string,
  row: MarketBoardRow,
) {
  const rule = rules?.[className];

  if (typeof rule !== 'function') {
    throw new Error(`Missing cellClassRule function for ${className}`);
  }

  return rule({ data: row } as CellClassParams<MarketBoardRow>);
}
