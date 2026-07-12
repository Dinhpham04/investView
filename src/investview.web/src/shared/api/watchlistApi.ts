import { deleteRequest, getJson, postJson } from './httpClient';
import type { AddWatchlistItemRequest, WatchlistItem } from '../types/watchlist';

export function getWatchlist(accessToken: string) {
  return getJson<WatchlistItem[]>('/api/watchlist', {
    headers: authorizationHeaders(accessToken),
  });
}

export function addWatchlistItem(accessToken: string, request: AddWatchlistItemRequest) {
  return postJson<WatchlistItem, AddWatchlistItemRequest>('/api/watchlist', request, {
    headers: authorizationHeaders(accessToken),
  });
}

export function removeWatchlistItem(
  accessToken: string,
  item: Pick<WatchlistItem, 'boardId' | 'symbol'>,
) {
  return deleteRequest(
    `/api/watchlist/${encodeURIComponent(item.boardId)}/${encodeURIComponent(item.symbol)}`,
    {
      headers: authorizationHeaders(accessToken),
    },
  );
}

function authorizationHeaders(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`,
  };
}
