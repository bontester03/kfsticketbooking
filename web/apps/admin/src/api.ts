import { createApiClient, endpoints } from '@kfs/api-client';

export const http = createApiClient();
export const api = {
  auth:   endpoints.auth(http),
  events: endpoints.events(http),
  admin:  endpoints.admin(http)
};
