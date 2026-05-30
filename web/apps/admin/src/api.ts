import { createApiClient, endpoints } from '@kfs/api-client';
import { useEventContext } from './lib/eventContext';

const viteEnv = (import.meta as unknown as { env?: Record<string, string> }).env;
const runtimeConfig = (globalThis as unknown as { __KFS_CONFIG__?: { apiBaseUrl?: string } }).__KFS_CONFIG__;

export const http = createApiClient({ baseURL: runtimeConfig?.apiBaseUrl ?? viteEnv?.VITE_API_BASE_URL ?? '/api/v1' });

// Inject eventId on every /admin/* request from the current EventContext.
// Skipped for /admin/events/* (the picker itself) so it can fetch the list without a context.
//
// - GET / DELETE: added as `?eventId=` query param
// - POST / PUT  : merged into the JSON body (so endpoints like /admin/passes/generate
//   and /admin/passes/quota pick it up without each caller having to read the store)
http.interceptors.request.use((config) => {
  if (!config.url?.includes('/admin/')) return config;
  if (config.url?.match(/\/admin\/events(\/|$|\?)/)) return config;

  const eventId = useEventContext.getState().eventId;
  if (!eventId) return config;

  const method = (config.method ?? 'get').toLowerCase();
  if (method === 'post' || method === 'put' || method === 'patch') {
    if (config.data && typeof config.data === 'object' && !(config.data instanceof FormData)) {
      const body = config.data as Record<string, unknown>;
      if (body.eventId == null) body.eventId = eventId;
    } else {
      // No body at all — fall through to the query-param branch.
      config.params = { ...(config.params ?? {}), eventId };
    }
  } else {
    config.params = { ...(config.params ?? {}), eventId };
  }
  return config;
});

export const api = {
  auth:   endpoints.auth(http),
  events: endpoints.events(http),
  admin:  endpoints.admin(http)
};
