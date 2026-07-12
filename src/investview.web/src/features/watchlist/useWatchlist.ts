import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { demoLogin, type DemoSession } from '../../shared/api/authApi';
import { addWatchlistItem, getWatchlist, removeWatchlistItem } from '../../shared/api/watchlistApi';
import type { AddWatchlistItemRequest, WatchlistItem } from '../../shared/types/watchlist';

const demoSessionStorageKey = 'investview.demoSession';

export function useWatchlist() {
  const queryClient = useQueryClient();
  const [session, setSession] = useState<DemoSession | null>(readStoredDemoSession);
  const accessToken = session?.accessToken ?? null;
  const queryKey = ['watchlist', accessToken];

  const watchlistQuery = useQuery({
    queryKey,
    queryFn: () => getWatchlist(accessToken ?? ''),
    enabled: accessToken != null,
  });

  const loginMutation = useMutation({
    mutationFn: demoLogin,
    onSuccess: (nextSession) => {
      setSession(nextSession);
      storeDemoSession(nextSession);
      queryClient.invalidateQueries({ queryKey: ['watchlist', nextSession.accessToken] });
    },
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
    error: watchlistQuery.error ?? loginMutation.error ?? addMutation.error ?? removeMutation.error,
    isAdding: addMutation.isPending,
    isLoading: accessToken != null && watchlistQuery.isPending,
    isLoggingIn: loginMutation.isPending,
    isRemoving: removeMutation.isPending,
    items: watchlistQuery.data ?? [],
    login: loginMutation.mutateAsync,
    removeItem: removeMutation.mutateAsync,
    session,
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

function readStoredDemoSession() {
  const storage = getLocalStorage();
  if (storage == null) {
    return null;
  }

  try {
    const storedValue = storage.getItem(demoSessionStorageKey);
    if (storedValue == null) {
      return null;
    }

    const session = JSON.parse(storedValue) as DemoSession;
    const expiresAt = Date.parse(session.expiresAt);
    if (!session.accessToken || !Number.isFinite(expiresAt) || expiresAt <= Date.now()) {
      storage.removeItem(demoSessionStorageKey);
      return null;
    }

    return session;
  } catch {
    return null;
  }
}

function storeDemoSession(session: DemoSession) {
  const storage = getLocalStorage();
  if (storage == null) {
    return;
  }

  try {
    storage.setItem(demoSessionStorageKey, JSON.stringify(session));
  } catch {
    // Ignore storage failures; the in-memory session remains usable.
  }
}

function getLocalStorage() {
  if (typeof window === 'undefined' || !('localStorage' in window)) {
    return null;
  }

  try {
    return window.localStorage;
  } catch {
    return null;
  }
}
