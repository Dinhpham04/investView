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
    cellClass: (params) => classForCell(params, 'symbolClass', 'market-cell--symbol'),
    tooltipField: 'displayName',
  },
      priceColumn('Trần', 'ceilingPrice', 'ceilingPriceClass'),
      priceColumn('Sàn', 'floorPrice', 'floorPriceClass'),
      priceColumn('TC', 'referencePrice', 'referencePriceClass'),
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
      priceColumn('Giá', 'lastPrice', 'lastPriceClass'),
      quantityColumn('KL', 'lastQuantity', 'lastQuantityClass'),
      changeColumn('+/-', 'change'),
      percentColumn('+/- (%)', 'changePercent'),
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
  quantityColumn('Tổng KL', 'totalVolume', 104),
  priceColumn('Cao', 'highPrice', 'highPriceClass'),
  priceColumn('Thấp', 'lowPrice', 'lowPriceClass'),
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
): ColDef<MarketBoardRow> {
  return {
    headerName,
    field,
    width,
    valueFormatter: priceValueFormatter,
    cellClass: (params) => classForCell(params, classField),
    cellClassRules: flashClassRules(field as MarketBoardFlashField),
  };
}

function quantityColumn(
  headerName: string,
  field: keyof MarketBoardRow,
  classFieldOrWidth?: keyof MarketBoardRow | number,
  width = 84,
): ColDef<MarketBoardRow> {
  const classField = typeof classFieldOrWidth === 'string' ? classFieldOrWidth : undefined;
  const columnWidth = typeof classFieldOrWidth === 'number' ? classFieldOrWidth : width;
  const column: ColDef<MarketBoardRow> = {
    headerName,
    field,
    width: columnWidth,
    valueFormatter: quantityValueFormatter,
  };

  column.cellClass = (params) => classForCell(params, classField);
  column.cellClassRules = flashClassRules(field as MarketBoardFlashField);

  return column;
}

function changeColumn(headerName: string, field: keyof MarketBoardRow): ColDef<MarketBoardRow> {
  return {
    headerName,
    field,
    width: 76,
    valueFormatter: (params) => formatChange(params.value as number | null | undefined),
    cellClass: (params) => classForCell(params, 'changeClass'),
    cellClassRules: flashClassRules('change'),
  };
}

function percentColumn(headerName: string, field: keyof MarketBoardRow): ColDef<MarketBoardRow> {
  return {
    headerName,
    field,
    width: 88,
    valueFormatter: (params) => formatPercent(params.value as number | null | undefined),
    cellClass: (params) => classForCell(params, 'changeClass'),
    cellClassRules: flashClassRules('changePercent'),
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

function classForCell(
  params: CellClassParams<MarketBoardRow>,
  classField?: keyof MarketBoardRow,
  extraClass?: string,
) {
  const priceClass = classField == null ? 'neutral' : (params.data?.[classField] as PriceClass | undefined);
  return ['market-cell', extraClass ?? 'market-cell--number', priceClassNames[priceClass ?? 'neutral']];
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
