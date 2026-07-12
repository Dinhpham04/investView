import { useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { demoLogin, type DemoSession } from '../../shared/api/authApi';
import { DemoSessionContext } from './demoSessionContext';
import type { DemoSessionContextValue } from './demoSessionContext';

const demoSessionStorageKey = 'investview.demoSession';

export function DemoSessionProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [session, setSession] = useState<DemoSession | null>(readStoredDemoSession);
  const loginMutation = useMutation({
    mutationFn: demoLogin,
    onSuccess: (nextSession) => {
      setSession(nextSession);
      storeDemoSession(nextSession);
      void queryClient.invalidateQueries({ queryKey: ['watchlist'] });
      void queryClient.invalidateQueries({ queryKey: ['portfolio'] });
      void queryClient.invalidateQueries({ queryKey: ['orders'] });
    },
  });

  const value = useMemo<DemoSessionContextValue>(() => ({
    error: loginMutation.error,
    isLoggingIn: loginMutation.isPending,
    login: loginMutation.mutateAsync,
    session,
  }), [loginMutation.error, loginMutation.isPending, loginMutation.mutateAsync, session]);

  return (
    <DemoSessionContext.Provider value={value}>
      {children}
    </DemoSessionContext.Provider>
  );
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
