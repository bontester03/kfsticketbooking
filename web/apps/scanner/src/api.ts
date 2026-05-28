import { createApiClient, endpoints } from '@kfs/api-client';

export const http = createApiClient();
export const api = {
  scan: endpoints.scan(http)
};
