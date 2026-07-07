export type SystemMarketListKind = 'exchange' | 'index';

export type SystemMarketList = {
  id: string;
  code: string;
  label: string;
  kind: SystemMarketListKind;
  dnseMarketId?: 'STO' | 'STX' | 'UPX';
  dnseIndexName?: string;
};

// DNSE market data enums:
// - marketId: STO = HOSE stocks, STX = HNX stocks, UPX = UPCOM stocks.
// - marketIndex: VNINDEX, HNX30, VN30, VN100, VNXALLSHARE, VNMITECH, VN50GROWTH, VNDIVIDEND.
export const systemExchangeLists: SystemMarketList[] = [
  { id: 'exchange-hose', code: 'HOSE', label: 'HOSE', kind: 'exchange', dnseMarketId: 'STO' },
  { id: 'exchange-hnx', code: 'HNX', label: 'HNX', kind: 'exchange', dnseMarketId: 'STX' },
  { id: 'exchange-upcom', code: 'UPCOM', label: 'UPCOM', kind: 'exchange', dnseMarketId: 'UPX' },
];

export const systemIndexLists: SystemMarketList[] = [
  { id: 'index-vnindex', code: 'VNINDEX', label: 'VNINDEX', kind: 'index', dnseIndexName: 'VNINDEX' },
  { id: 'index-hnx30', code: 'HNX30', label: 'HNX30', kind: 'index', dnseIndexName: 'HNX30' },
  { id: 'index-vn30', code: 'VN30', label: 'VN30', kind: 'index', dnseIndexName: 'VN30' },
  { id: 'index-vn100', code: 'VN100', label: 'VN100', kind: 'index', dnseIndexName: 'VN100' },
  { id: 'index-vnxallshare', code: 'VNXALLSHARE', label: 'VNXALLSHARE', kind: 'index', dnseIndexName: 'VNXALLSHARE' },
  { id: 'index-vnmitech', code: 'VNMITECH', label: 'VNMITECH', kind: 'index', dnseIndexName: 'VNMITECH' },
  { id: 'index-vn50growth', code: 'VN50GROWTH', label: 'VN50GROWTH', kind: 'index', dnseIndexName: 'VN50GROWTH' },
  { id: 'index-vndividend', code: 'VNDIVIDEND', label: 'VNDIVIDEND', kind: 'index', dnseIndexName: 'VNDIVIDEND' },
];

export const systemMarketLists: SystemMarketList[] = [...systemExchangeLists, ...systemIndexLists];
