export type WatchlistItem = {
  id: string;
  symbol: string;
  boardId: string;
  createdAt: string;
};

export type AddWatchlistItemRequest = {
  symbol: string;
  boardId: string;
};
