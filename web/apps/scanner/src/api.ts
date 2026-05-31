import { createApiClient, endpoints } from '@kfs/api-client';

const viteEnv = (import.meta as unknown as { env?: Record<string, string> }).env;
const runtimeConfig = (globalThis as unknown as { __KFS_CONFIG__?: { apiBaseUrl?: string } }).__KFS_CONFIG__;

export const http = createApiClient({ baseURL: runtimeConfig?.apiBaseUrl ?? viteEnv?.VITE_API_BASE_URL ?? '/api/v1' });
export const api = {
  scan: endpoints.scan(http)
};
