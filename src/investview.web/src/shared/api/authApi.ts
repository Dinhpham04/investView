import { authorizationHeaders } from './authorizationHeaders';
import { getJson, postJson } from './httpClient';

const demoCredentials = {
  email: 'demo@investview.local',
  password: 'demo-password',
};

export type DemoSession = {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  user: {
    id: string;
    email: string;
    displayName: string;
  };
};

export type DemoProfile = DemoSession['user'] & {
  cashAccounts: Array<{
    currency: string;
    balance: number;
    availableBalance: number;
  }>;
};

export function demoLogin() {
  return postJson<DemoSession, typeof demoCredentials>('/api/auth/demo-login', demoCredentials);
}

export function getDemoProfile(accessToken: string, signal?: AbortSignal) {
  return getJson<DemoProfile>('/api/me', {
    headers: authorizationHeaders(accessToken),
    signal,
  });
}
