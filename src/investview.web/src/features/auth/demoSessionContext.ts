import { createContext } from 'react';
import type { DemoSession } from '../../shared/api/authApi';

export type DemoSessionStatus = 'checking' | 'guest' | 'authenticated';

export type DemoSessionContextValue = {
  error: Error | null;
  isLoggingIn: boolean;
  login: () => Promise<DemoSession>;
  logout: () => void;
  session: DemoSession | null;
  status: DemoSessionStatus;
};

export const DemoSessionContext = createContext<DemoSessionContextValue | null>(null);
