import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { demoLogin, getDemoProfile, type DemoSession } from '../../shared/api/authApi';
import { subscribeToUnauthorized } from '../../shared/api/httpClient';
import { DemoSessionContext } from './demoSessionContext';
import type { DemoSessionContextValue, DemoSessionStatus } from './demoSessionContext';

const demoSessionStorageKey = 'investview.demoSession';

export function DemoSessionProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [storedSession, setStoredSession] = useState<DemoSession | null>(readStoredDemoSession);
  const [status, setStatus] = useState<DemoSessionStatus>(() => storedSession == null ? 'guest' : 'checking');
  const logout = useCallback(() => {
    setStoredSession(null);
    setStatus('guest');
    removeStoredDemoSession();
    queryClient.removeQueries({ queryKey: ['watchlist'] });
    queryClient.removeQueries({ queryKey: ['portfolio'] });
    queryClient.removeQueries({ queryKey: ['orders'] });
  }, [queryClient]);
  const loginMutation = useMutation({
    mutationFn: demoLogin,
    onSuccess: (nextSession) => {
      setStoredSession(nextSession);
      setStatus('authenticated');
      storeDemoSession(nextSession);
      void queryClient.invalidateQueries({ queryKey: ['watchlist'] });
      void queryClient.invalidateQueries({ queryKey: ['portfolio'] });
      void queryClient.invalidateQueries({ queryKey: ['orders'] });
    },
  });

  useEffect(() => subscribeToUnauthorized(logout), [logout]);

  useEffect(() => {
    if (status !== 'checking' || storedSession == null) {
      return undefined;
    }

    let disposed = false;
    const controller = new AbortController();

    void getDemoProfile(storedSession.accessToken, controller.signal).then(
      (profile) => {
        if (disposed) {
          return;
        }

        const validatedSession: DemoSession = {
          ...storedSession,
          user: {
            id: profile.id,
            email: profile.email,
            displayName: profile.displayName,
          },
        };
        setStoredSession(validatedSession);
        setStatus('authenticated');
        storeDemoSession(validatedSession);
      },
      () => {
        if (!disposed) {
          logout();
        }
      },
    );

    return () => {
      disposed = true;
      controller.abort();
    };
  }, [logout, status, storedSession]);

  const session = status === 'authenticated' ? storedSession : null;

  const value = useMemo<DemoSessionContextValue>(() => ({
    error: loginMutation.error,
    isLoggingIn: loginMutation.isPending || status === 'checking',
    login: loginMutation.mutateAsync,
    logout,
    session,
    status,
  }), [loginMutation.error, loginMutation.isPending, loginMutation.mutateAsync, logout, session, status]);

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

function removeStoredDemoSession() {
  const storage = getLocalStorage();
  if (storage == null) {
    return;
  }

  try {
    storage.removeItem(demoSessionStorageKey);
  } catch {
    // Ignore storage failures; the in-memory session is still cleared.
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
