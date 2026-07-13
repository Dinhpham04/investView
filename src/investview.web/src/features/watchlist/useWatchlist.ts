import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { addWatchlistItem, getWatchlist, removeWatchlistItem } from '../../shared/api/watchlistApi';
import type { AddWatchlistItemRequest, WatchlistItem } from '../../shared/types/watchlist';
import { useDemoSession } from '../auth/useDemoSession';

export function useWatchlist() {
  const queryClient = useQueryClient();
  const { session, status } = useDemoSession();
  const accessToken = session?.accessToken ?? null;
  const queryKey = ['watchlist', accessToken];

  const watchlistQuery = useQuery({
    queryKey,
    queryFn: () => getWatchlist(accessToken ?? ''),
    enabled: accessToken != null,
  });

  const addMutation = useMutation({
    mutationFn: (request: AddWatchlistItemRequest) => {
      if (accessToken == null) {
        throw new Error('Demo login is required.');
      }

      return addWatchlistItem(accessToken, request);
    },
    onSuccess: (item) => {
      queryClient.setQueryData<WatchlistItem[]>(queryKey, (existingItems = []) =>
        upsertWatchlistItem(existingItems, item),
      );
    },
  });

  const removeMutation = useMutation({
    mutationFn: (item: Pick<WatchlistItem, 'boardId' | 'symbol'>) => {
      if (accessToken == null) {
        throw new Error('Demo login is required.');
      }

      return removeWatchlistItem(accessToken, item);
    },
    onSuccess: (_, removedItem) => {
      queryClient.setQueryData<WatchlistItem[]>(queryKey, (existingItems = []) =>
        existingItems.filter((item) =>
          item.boardId !== removedItem.boardId || item.symbol !== removedItem.symbol,
        ),
      );
    },
  });

  return {
    addItem: addMutation.mutateAsync,
    error: watchlistQuery.error ?? addMutation.error ?? removeMutation.error,
    isAdding: addMutation.isPending,
    isLoading: accessToken != null && watchlistQuery.isPending,
    isRemoving: removeMutation.isPending,
    items: watchlistQuery.data ?? [],
    removeItem: removeMutation.mutateAsync,
    session,
    status,
  };
}

function upsertWatchlistItem(items: WatchlistItem[], nextItem: WatchlistItem) {
  const existingIndex = items.findIndex((item) =>
    item.boardId === nextItem.boardId && item.symbol === nextItem.symbol,
  );
  if (existingIndex < 0) {
    return [...items, nextItem].sort(compareWatchlistItems);
  }

  return items.map((item, index) => index === existingIndex ? nextItem : item);
}

function compareWatchlistItems(left: WatchlistItem, right: WatchlistItem) {
  return left.symbol.localeCompare(right.symbol) || left.boardId.localeCompare(right.boardId);
}
