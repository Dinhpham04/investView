import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { addWatchlistItem, createWatchlistGroup, getWatchlist, removeWatchlistItem } from '../../shared/api/watchlistApi';
import type { AddWatchlistItemRequest, CreateWatchlistGroupRequest, WatchlistGroup, WatchlistItem } from '../../shared/types/watchlist';
import { useDemoSession } from '../auth/useDemoSession';

export type AddWatchlistItemToGroupRequest = AddWatchlistItemRequest & {
  groupId: string;
};

export type RemoveWatchlistItemFromGroupRequest = Pick<WatchlistItem, 'boardId' | 'symbol'> & {
  groupId: string;
};

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

  const createGroupMutation = useMutation({
    mutationFn: (request: CreateWatchlistGroupRequest) => {
      if (accessToken == null) {
        throw new Error('Demo login is required.');
      }

      return createWatchlistGroup(accessToken, request);
    },
    onSuccess: (group) => {
      queryClient.setQueryData<WatchlistGroup[]>(queryKey, (existingGroups = []) =>
        upsertWatchlistGroup(existingGroups, group),
      );
    },
  });

  const addMutation = useMutation({
    mutationFn: (request: AddWatchlistItemToGroupRequest) => {
      if (accessToken == null) {
        throw new Error('Demo login is required.');
      }

      return addWatchlistItem(accessToken, request.groupId, {
        boardId: request.boardId,
        symbol: request.symbol,
      });
    },
    onSuccess: (item) => {
      queryClient.setQueryData<WatchlistGroup[]>(queryKey, (existingGroups = []) =>
        existingGroups.map((group) =>
          group.id === item.groupId
            ? { ...group, items: upsertWatchlistItem(group.items, item) }
            : group,
        ),
      );
    },
  });

  const removeMutation = useMutation({
    mutationFn: (item: RemoveWatchlistItemFromGroupRequest) => {
      if (accessToken == null) {
        throw new Error('Demo login is required.');
      }

      return removeWatchlistItem(accessToken, item.groupId, item);
    },
    onSuccess: (_, removedItem) => {
      queryClient.setQueryData<WatchlistGroup[]>(queryKey, (existingGroups = []) =>
        existingGroups.map((group) =>
          group.id === removedItem.groupId
            ? {
              ...group,
              items: group.items.filter((item) =>
                item.boardId !== removedItem.boardId || item.symbol !== removedItem.symbol,
              ),
            }
            : group,
        ),
      );
    },
  });

  return {
    addItem: addMutation.mutateAsync,
    createGroup: createGroupMutation.mutateAsync,
    error: watchlistQuery.error ?? createGroupMutation.error ?? addMutation.error ?? removeMutation.error,
    groups: watchlistQuery.data ?? [],
    isAdding: addMutation.isPending,
    isCreatingGroup: createGroupMutation.isPending,
    isLoading: accessToken != null && watchlistQuery.isPending,
    isRemoving: removeMutation.isPending,
    removeItem: removeMutation.mutateAsync,
    session,
    status,
  };
}

function upsertWatchlistGroup(groups: WatchlistGroup[], nextGroup: WatchlistGroup) {
  const existingIndex = groups.findIndex((group) => group.id === nextGroup.id);
  if (existingIndex < 0) {
    return [...groups, nextGroup].sort(compareWatchlistGroups);
  }

  return groups.map((group, index) => index === existingIndex ? nextGroup : group);
}

function compareWatchlistGroups(left: WatchlistGroup, right: WatchlistGroup) {
  return left.name.localeCompare(right.name);
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
