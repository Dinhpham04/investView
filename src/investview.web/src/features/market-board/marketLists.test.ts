import { describe, expect, it } from 'vitest';
import { systemExchangeLists, systemIndexLists, systemMarketLists } from './marketLists';

describe('market lists', () => {
  it('maps exchange tabs to DNSE market ids', () => {
    expect(systemExchangeLists).toEqual([
      { id: 'exchange-hose', code: 'HOSE', label: 'HOSE', kind: 'exchange', dnseMarketId: 'STO' },
      { id: 'exchange-hnx', code: 'HNX', label: 'HNX', kind: 'exchange', dnseMarketId: 'STX' },
      { id: 'exchange-upcom', code: 'UPCOM', label: 'UPCOM', kind: 'exchange', dnseMarketId: 'UPX' },
    ]);
  });

  it('uses documented DNSE market index names for index tabs', () => {
    expect(systemIndexLists.map((item) => item.dnseIndexName)).toEqual([
      'VNINDEX',
      'HNX30',
      'VN30',
      'VN100',
      'VNXALLSHARE',
      'VNMITECH',
      'VN50GROWTH',
      'VNDIVIDEND',
    ]);
  });

  it('keeps unique ids for all system market lists', () => {
    const ids = systemMarketLists.map((item) => item.id);

    expect(new Set(ids).size).toBe(ids.length);
  });
});
