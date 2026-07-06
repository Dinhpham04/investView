import type { CellClassParams, ColDef, ColGroupDef, ValueFormatterParams } from 'ag-grid-community';
import {
  formatChange,
  formatPercent,
  formatPrice,
  formatQuantity,
  type MarketBoardRow,
  type PriceClass,
} from './marketBoardFormatters';

const priceClassNames: Record<PriceClass, string> = {
  ceiling: 'quote-price-ceiling',
  floor: 'quote-price-floor',
  reference: 'quote-price-reference',
  up: 'quote-price-up',
  down: 'quote-price-down',
  neutral: 'quote-price-neutral',
};

export const defaultMarketBoardColumnDef: ColDef<MarketBoardRow> = {
  sortable: false,
  resizable: true,
  suppressMovable: true,
  cellClass: 'market-cell market-cell--number',
  headerClass: 'market-header',
};

export const marketBoardColumnDefs: (ColDef<MarketBoardRow> | ColGroupDef<MarketBoardRow>)[] = [
  {
    headerName: 'CK',
    field: 'symbol',
    pinned: 'left',
    width: 112,
    lockPinned: true,
    cellClass: 'market-cell market-cell--symbol',
    tooltipField: 'displayName',
  },
  {
    headerName: 'Giá tham chiếu',
    marryChildren: true,
    children: [
      priceColumn('Trần', 'ceilingPrice', 'ceilingPriceClass'),
      priceColumn('Sàn', 'floorPrice', 'floorPriceClass'),
      priceColumn('TC', 'referencePrice', 'referencePriceClass'),
    ],
  },
  {
    headerName: 'Bên mua',
    marryChildren: true,
    children: [
      priceColumn('Giá 3', 'bid3Price', 'bid3PriceClass'),
      quantityColumn('KL 3', 'bid3Quantity'),
      priceColumn('Giá 2', 'bid2Price', 'bid2PriceClass'),
      quantityColumn('KL 2', 'bid2Quantity'),
      priceColumn('Giá 1', 'bid1Price', 'bid1PriceClass'),
      quantityColumn('KL 1', 'bid1Quantity'),
    ],
  },
  {
    headerName: 'Khớp lệnh',
    marryChildren: true,
    children: [
      priceColumn('Giá', 'lastPrice', 'lastPriceClass'),
      quantityColumn('KL', 'lastQuantity'),
      changeColumn('+/-', 'change'),
      percentColumn('+/- (%)', 'changePercent'),
    ],
  },
  {
    headerName: 'Bên bán',
    marryChildren: true,
    children: [
      priceColumn('Giá 1', 'ask1Price', 'ask1PriceClass'),
      quantityColumn('KL 1', 'ask1Quantity'),
      priceColumn('Giá 2', 'ask2Price', 'ask2PriceClass'),
      quantityColumn('KL 2', 'ask2Quantity'),
      priceColumn('Giá 3', 'ask3Price', 'ask3PriceClass'),
      quantityColumn('KL 3', 'ask3Quantity'),
    ],
  },
  quantityColumn('Tổng KL', 'totalVolume', 104),
  priceColumn('Cao', 'highPrice', 'highPriceClass'),
  priceColumn('Thấp', 'lowPrice', 'lowPriceClass'),
  {
    headerName: 'TT',
    field: 'tradingStatus',
    width: 112,
    cellClass: 'market-cell market-cell--status',
  },
  {
    headerName: 'Cập nhật',
    field: 'updatedTime',
    width: 96,
    cellClass: 'market-cell market-cell--time',
  },
];

function priceColumn(
  headerName: string,
  field: keyof MarketBoardRow,
  classField: keyof MarketBoardRow,
  width = 76,
): ColDef<MarketBoardRow> {
  return {
    headerName,
    field,
    width,
    valueFormatter: priceValueFormatter,
    cellClass: (params) => classForPrice(params, classField),
  };
}

function quantityColumn(headerName: string, field: keyof MarketBoardRow, width = 84): ColDef<MarketBoardRow> {
  return {
    headerName,
    field,
    width,
    valueFormatter: quantityValueFormatter,
  };
}

function changeColumn(headerName: string, field: keyof MarketBoardRow): ColDef<MarketBoardRow> {
  return {
    headerName,
    field,
    width: 76,
    valueFormatter: (params) => formatChange(params.value as number | null | undefined),
    cellClass: (params) => classForPrice(params, 'changeClass'),
  };
}

function percentColumn(headerName: string, field: keyof MarketBoardRow): ColDef<MarketBoardRow> {
  return {
    headerName,
    field,
    width: 88,
    valueFormatter: (params) => formatPercent(params.value as number | null | undefined),
    cellClass: (params) => classForPrice(params, 'changeClass'),
  };
}

function priceValueFormatter(params: ValueFormatterParams<MarketBoardRow>) {
  return formatPrice(params.value as number | null | undefined);
}

function quantityValueFormatter(params: ValueFormatterParams<MarketBoardRow>) {
  return formatQuantity(params.value as number | null | undefined);
}

function classForPrice(params: CellClassParams<MarketBoardRow>, classField: keyof MarketBoardRow) {
  const priceClass = params.data?.[classField] as PriceClass | undefined;
  return ['market-cell', 'market-cell--number', priceClassNames[priceClass ?? 'neutral']];
}
