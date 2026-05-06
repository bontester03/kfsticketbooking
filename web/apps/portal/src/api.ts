import { createApiClient, endpoints } from '@kfs/api-client';

export const http = createApiClient();
export const api = {
  auth:     endpoints.auth(http),
  events:   endpoints.events(http),
  cart:     endpoints.cart(http),
  bookings: endpoints.bookings(http)
};
