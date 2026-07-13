import { deleteRequest, getJson, postJson } from './httpClient';
import { authorizationHeaders } from './authorizationHeaders';
import type { AddWatchlistItemRequest, CreateWatchlistGroupRequest, WatchlistGroup, WatchlistItem } from '../types/watchlist';

export function getWatchlist(accessToken: string) {
  return getJson<WatchlistGroup[]>('/api/watchlist', {
    headers: authorizationHeaders(accessToken),
  });
}

export function createWatchlistGroup(accessToken: string, request: CreateWatchlistGroupRequest) {
  return postJson<WatchlistGroup, CreateWatchlistGroupRequest>('/api/watchlist', request, {
    headers: authorizationHeaders(accessToken),
  });
}

export function addWatchlistItem(accessToken: string, groupId: string, request: AddWatchlistItemRequest) {
  return postJson<WatchlistItem, AddWatchlistItemRequest>(`/api/watchlist/${encodeURIComponent(groupId)}/items`, request, {
    headers: authorizationHeaders(accessToken),
  });
}

export function removeWatchlistItem(
  accessToken: string,
  groupId: string,
  item: Pick<WatchlistItem, 'boardId' | 'symbol'>,
) {
  return deleteRequest(
    `/api/watchlist/${encodeURIComponent(groupId)}/items/${encodeURIComponent(item.boardId)}/${encodeURIComponent(item.symbol)}`,
    {
      headers: authorizationHeaders(accessToken),
    },
  );
}
