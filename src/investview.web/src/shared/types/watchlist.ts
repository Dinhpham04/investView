export type WatchlistGroup = {
  id: string;
  name: string;
  createdAt: string;
  updatedAt: string;
  items: WatchlistItem[];
};

export type WatchlistItem = {
  id: string;
  groupId: string;
  symbol: string;
  boardId: string;
  createdAt: string;
};

export type CreateWatchlistGroupRequest = {
  name: string;
};

export type AddWatchlistItemRequest = {
  symbol: string;
  boardId: string;
};
