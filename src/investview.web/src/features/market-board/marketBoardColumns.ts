import type { CellClassParams, CellClassRules, ColDef, ColGroupDef, ValueFormatterParams } from 'ag-grid-community';
import {
  formatChange,
  formatPercent,
  formatPrice,
  formatQuantity,
  type MarketBoardFlashField,
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

const flashClassNames: Record<PriceClass, string> = {
  ceiling: 'quote-flash-ceiling',
  floor: 'quote-flash-floor',
  reference: 'quote-flash-reference',
  up: 'quote-flash-up',
  down: 'quote-flash-down',
  neutral: 'quote-flash-neutral',
};

export const defaultMarketBoardColumnDef: ColDef<MarketBoardRow> = {
  sortable: true,
  resizable: true,
  suppressMovable: true,
  minWidth: 40,
  cellClass: 'market-cell market-cell--number',
  headerClass: 'header-cell-label-right',
};

export const defaultMarketBoardColumnGroupDef = {
  headerClass: 'header-group-cell-label-center',
};

export const marketBoardColumnDefs: (ColDef<MarketBoardRow> | ColGroupDef<MarketBoardRow>)[] = [
  {
    colId: 'pinSymbol',
    headerName: '',
    pinned: 'left',
    width: 28,
    minWidth: 28,
    maxWidth: 28,
    lockPinned: true,
    resizable: false,
    sortable: false,
    cellClass: 'market-cell market-cell--pin',
    cellRenderer: () => '☆',
  },
  {
    headerName: 'CK',
    headerClass: 'flex-start',
    field: 'symbol',
    pinned: 'left',
    width: 72,
    lockPinned: true,
    sortable: true,
    comparator: symbolComparator,
    cellClass: staticClasses('market-cell--symbol'),
    cellClassRules: priceClassRules('symbolClass'),
    tooltipField: 'displayName',
  },
      priceColumn('Trần', 'ceilingPrice', 'ceilingPriceClass', undefined, true),
      priceColumn('Sàn', 'floorPrice', 'floorPriceClass', undefined, true),
      priceColumn('TC', 'referencePrice', 'referencePriceClass', undefined, true),
  {
    headerName: 'Bên mua',
    marryChildren: true,
    children: [
      priceColumn('Giá 3', 'bid3Price', 'bid3PriceClass'),
      quantityColumn('KL 3', 'bid3Quantity', 'bid3QuantityClass'),
      priceColumn('Giá 2', 'bid2Price', 'bid2PriceClass'),
      quantityColumn('KL 2', 'bid2Quantity', 'bid2QuantityClass'),
      priceColumn('Giá 1', 'bid1Price', 'bid1PriceClass'),
      quantityColumn('KL 1', 'bid1Quantity', 'bid1QuantityClass'),
    ],
  },
  {
    headerName: 'Khớp lệnh',
    marryChildren: true,
    children: [
      priceColumn('Giá', 'lastPrice', 'lastPriceClass', undefined, true),
      quantityColumn('KL', 'lastQuantity', 'lastQuantityClass', undefined, true),
      changeColumn('+/-', 'change', true),
      percentColumn('+/- (%)', 'changePercent', true),
    ],
  },
  {
    headerName: 'Bên bán',
    marryChildren: true,
    children: [
      priceColumn('Giá 1', 'ask1Price', 'ask1PriceClass'),
      quantityColumn('KL 1', 'ask1Quantity', 'ask1QuantityClass'),
      priceColumn('Giá 2', 'ask2Price', 'ask2PriceClass'),
      quantityColumn('KL 2', 'ask2Quantity', 'ask2QuantityClass'),
      priceColumn('Giá 3', 'ask3Price', 'ask3PriceClass'),
      quantityColumn('KL 3', 'ask3Quantity', 'ask3QuantityClass'),
    ],
  },
  quantityColumn('Tổng KL', 'totalVolume', 104, undefined, true),
  priceColumn('Cao', 'highPrice', 'highPriceClass', undefined, true),
  priceColumn('Thấp', 'lowPrice', 'lowPriceClass', undefined, true),
  {
    headerName: 'ĐTNN',
    marryChildren: true,
    children: [
      quantityColumn('NN mua', 'foreignBuyVolume', 92),
      quantityColumn('NN bán', 'foreignSellVolume', 92),
      quantityColumn('Room', 'foreignRoom', 112),
    ],
  },
];

function priceColumn(
  headerName: string,
  field: keyof MarketBoardRow,
  classField: keyof MarketBoardRow,
  width = 76,
  highlight = false
): ColDef<MarketBoardRow> {
  return {
    headerName,
    field,
    width,
    valueFormatter: priceValueFormatter,
    cellClass: staticClasses(undefined, highlight),
    cellClassRules: {
      ...priceClassRules(classField),
      ...flashClassRules(field as MarketBoardFlashField),
    },
  };
}

function quantityColumn(
  headerName: string,
  field: keyof MarketBoardRow,
  classFieldOrWidth?: keyof MarketBoardRow | number,
  width = 84,
  highlight = false
): ColDef<MarketBoardRow> {
  const classField = typeof classFieldOrWidth === 'string' ? classFieldOrWidth : undefined;
  const columnWidth = typeof classFieldOrWidth === 'number' ? classFieldOrWidth : width;
  return {
    headerName,
    field,
    width: columnWidth,
    valueFormatter: quantityValueFormatter,
    cellClass: staticClasses(undefined, highlight),
    cellClassRules: {
      ...priceClassRules(classField),
      ...flashClassRules(field as MarketBoardFlashField),
    },
  };
}

function changeColumn(headerName: string, field: keyof MarketBoardRow, highlight = false): ColDef<MarketBoardRow> {
  return {
    headerName,
    field,
    width: 76,
    valueFormatter: (params) => formatChange(params.value as number | null | undefined, params.data?.referencePrice),
    cellClass: staticClasses(undefined, highlight),
    cellClassRules: {
      ...priceClassRules('changeClass'),
      ...flashClassRules('change'),
    },
  };
}

function percentColumn(headerName: string, field: keyof MarketBoardRow, highlight = false): ColDef<MarketBoardRow> {
  return {
    headerName,
    field,
    width: 88,
    valueFormatter: (params) => formatPercent(params.value as number | null | undefined),
    cellClass: staticClasses(undefined, highlight),
    cellClassRules: {
      ...priceClassRules('changeClass'),
      ...flashClassRules('changePercent'),
    },
  };
}

function symbolComparator(valueA: string | null | undefined, valueB: string | null | undefined) {
  return (valueA ?? '').localeCompare(valueB ?? '', 'en', { sensitivity: 'base' });
}

function priceValueFormatter(params: ValueFormatterParams<MarketBoardRow>) {
  return formatPrice(params.value as number | null | undefined);
}

function quantityValueFormatter(params: ValueFormatterParams<MarketBoardRow>) {
  return formatQuantity(params.value as number | null | undefined);
}

function staticClasses(extraClass?: string, highlight = false) {
  return [
    'market-cell',
    extraClass ?? 'market-cell--number',
    highlight ? 'ag-cell-bg-highlight' : ''
  ];
}

function priceClassRules(classField?: keyof MarketBoardRow): CellClassRules<MarketBoardRow> {
  if (!classField) {
    return { [priceClassNames.neutral]: () => true };
  }
  return {
    [priceClassNames.ceiling]: (params) => params.data?.[classField] === 'ceiling',
    [priceClassNames.floor]: (params) => params.data?.[classField] === 'floor',
    [priceClassNames.reference]: (params) => params.data?.[classField] === 'reference',
    [priceClassNames.up]: (params) => params.data?.[classField] === 'up',
    [priceClassNames.down]: (params) => params.data?.[classField] === 'down',
    [priceClassNames.neutral]: (params) => params.data?.[classField] === 'neutral' || params.data?.[classField] == null,
  };
}

function flashClassRules(flashField: MarketBoardFlashField): CellClassRules<MarketBoardRow> {
  return {
    'quote-cell-flash': (params) => params.data?.flashClasses[flashField] != null,
    [flashClassNames.ceiling]: (params) => params.data?.flashClasses[flashField] === 'ceiling',
    [flashClassNames.floor]: (params) => params.data?.flashClasses[flashField] === 'floor',
    [flashClassNames.reference]: (params) => params.data?.flashClasses[flashField] === 'reference',
    [flashClassNames.up]: (params) => params.data?.flashClasses[flashField] === 'up',
    [flashClassNames.down]: (params) => params.data?.flashClasses[flashField] === 'down',
    [flashClassNames.neutral]: (params) => params.data?.flashClasses[flashField] === 'neutral',
  };
}
