import { postJson } from './httpClient';

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

export function demoLogin() {
  return postJson<DemoSession, typeof demoCredentials>('/api/auth/demo-login', demoCredentials);
}
