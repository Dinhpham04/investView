import { getJson } from './httpClient';
import type { HealthResponse } from '../types/health';

export function getHealth() {
  return getJson<HealthResponse>('/health');
}
