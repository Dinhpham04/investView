import { createContext } from 'react';
import type { DemoSession } from '../../shared/api/authApi';

export type DemoSessionContextValue = {
  error: Error | null;
  isLoggingIn: boolean;
  login: () => Promise<DemoSession>;
  session: DemoSession | null;
};

export const DemoSessionContext = createContext<DemoSessionContextValue | null>(null);
