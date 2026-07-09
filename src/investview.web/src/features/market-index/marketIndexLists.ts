export const defaultMarketIndexNames = ['VNINDEX', 'VN30', 'HNX', 'HNX30', 'UPCOM'] as const;

export type DefaultMarketIndexName = (typeof defaultMarketIndexNames)[number];
