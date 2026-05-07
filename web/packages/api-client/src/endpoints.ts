import type { AxiosInstance } from 'axios';
import type {
  AuthResponse, BookingDto, EventDto, SeatMapDto, ZoneGroup, ZoneSide
} from '@kfs/types';

// Strongly-typed endpoint adapters. Each takes the Axios client (so apps can
// inject their own — useful for tests with MSW). Manual today; OpenAPI codegen
// is the spec's preferred path and tracked in DECISIONS.md as a follow-up.

export const endpoints = {
  auth: (c: AxiosInstance) => ({
    login: (email: string, password: string) =>
      c.post<AuthResponse>('/auth/login', { email, password }).then(r => r.data),
    adminLogin: (email: string, password: string) =>
      c.post<AuthResponse>('/auth/admin/login', { email, password }).then(r => r.data),
    refresh: (refreshToken: string) =>
      c.post<AuthResponse>('/auth/refresh', { refreshToken }).then(r => r.data),
    changePassword: (currentPassword: string, newPassword: string) =>
      c.post<void>('/auth/change-password', { currentPassword, newPassword }).then(r => r.data)
  }),

  events: (c: AxiosInstance) => ({
    active: () => c.get<EventDto>('/events/active').then(r => r.data),
    seatMap: (eventId: string, group: ZoneGroup) =>
      c.get<SeatMapDto>(`/events/${eventId}/seatmap`, { params: { group } }).then(r => r.data)
  }),

  cart: (c: AxiosInstance) => ({
    // The API returns 204 No Content when there's no cart. Axios then leaves r.data
    // undefined, which TanStack Query v5 rejects as a query result — coerce to null so
    // queries downstream can branch on a real falsy value instead of crashing the app.
    get: () => c.get<BookingDto | null>('/cart').then(r => r.data ?? null),
    select: (group: ZoneGroup, side: ZoneSide, rowLabel: string, seatNumber: number) =>
      c.post<BookingDto>('/cart/select', { group, side, rowLabel, seatNumber }).then(r => r.data),
    release: () => c.delete<void>('/cart').then(r => r.data),
    checkout: () => c.post<BookingDto>('/cart/checkout').then(r => r.data)
  }),

  bookings: (c: AxiosInstance) => ({
    list: () => c.get<BookingDto[]>('/bookings').then(r => r.data),
    cancel: (id: string) => c.post<BookingDto>(`/bookings/${id}/cancel`).then(r => r.data),
    resendEmails: (id: string) => c.post<void>(`/bookings/${id}/resend-emails`).then(r => r.data)
  })
};
