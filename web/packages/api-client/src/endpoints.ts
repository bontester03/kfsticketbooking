import type { AxiosInstance } from 'axios';
import type {
  AuthResponse, BookingDto, EventDto, SeatMapDto, ZoneGroup, ZoneSide, PublicEventDto,
  StudentDto, StudentImportResultDto, ResetPasswordResponseDto, DashboardStatsDto,
  GeneratePassesRequest, GeneratePassesResponse, AdminPassDto, PassBatchSummaryDto,
  PassOutputFormat, AdminPassType, ReminderLogDto, UpdateEventRequest, BookingStatus,
  PassQuotaDto, GuestPassDto, GuestAnalyticsDto, GuestEligibleStudentDto, ScanResponse, ScanAuditDto
} from '@kfs/types';

// Triggers a browser download of a Blob response with the given filename.
function downloadBlob(data: Blob, filename: string) {
  const url = URL.createObjectURL(data);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

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
      c.get<SeatMapDto>(`/events/${eventId}/seatmap`, { params: { group } }).then(r => r.data),
    // Pre-auth: safe aggregate info for the public landing banner. 204 → null when no event.
    publicSummary: () =>
      c.get<PublicEventDto | null>('/public/event').then(r => r.status === 204 ? null : (r.data ?? null))
  }),

  cart: (c: AxiosInstance) => ({
    // API returns 204 No Content when there's no cart. Axios surfaces that as either
    // r.data === undefined OR r.data === '' depending on transformResponse — both crash
    // downstream code that does `cart?.items.find(...)`. Coerce strictly on status.
    get: () => c.get<BookingDto | null>('/cart').then(r => r.status === 204 ? null : (r.data ?? null)),
    select: (group: ZoneGroup, side: ZoneSide, rowLabel: string, seatNumber: number) =>
      c.post<BookingDto>('/cart/select', { group, side, rowLabel, seatNumber }).then(r => r.data),
    release: () => c.delete<void>('/cart').then(r => r.data),
    checkout: () => c.post<BookingDto>('/cart/checkout').then(r => r.data)
  }),

  bookings: (c: AxiosInstance) => ({
    list: () => c.get<BookingDto[]>('/bookings').then(r => r.data),
    cancel: (id: string) => c.post<BookingDto>(`/bookings/${id}/cancel`).then(r => r.data),
    resendEmails: (id: string) => c.post<void>(`/bookings/${id}/resend-emails`).then(r => r.data),
    // Combined PDF: every parent pass + the guest ticket (if any). Triggers a file download.
    downloadAllPdf: async () => {
      const resp = await c.get('/me/tickets.pdf', { responseType: 'blob' });
      const disp = (resp.headers as Record<string, string>)['content-disposition'] ?? '';
      const m = /filename="?([^"]+)"?/.exec(disp);
      downloadBlob(resp.data as Blob, m?.[1] ?? 'kfs-tickets.pdf');
    }
  }),

  // Student's own Guest ticket (1 QR, admits 3). 204 → null when not booked.
  guest: (c: AxiosInstance) => ({
    get: () => c.get<GuestPassDto | null>('/guest').then(r => r.status === 204 ? null : (r.data ?? null)),
    book: () => c.post<GuestPassDto>('/guest').then(r => r.data)
  }),

  // Public gate scanner — no auth; gated by the event scanner token.
  scan: (c: AxiosInstance) => ({
    verify: (qrPayload: string, eventToken: string, deviceInfo?: string) =>
      c.post<ScanResponse>('/scan/verify', { qrPayload, eventToken, deviceInfo }).then(r => r.data)
  }),

  admin: (c: AxiosInstance) => ({
    dashboard: () =>
      c.get<DashboardStatsDto>('/admin/reports/dashboard').then(r => r.data),

    // Live seat map for a group (A=1 / B=2). Admin variant of events.seatMap.
    seatMap: (group: ZoneGroup) =>
      c.get<SeatMapDto>('/admin/seatmap', { params: { group } }).then(r => r.data),

    bookings: (group?: ZoneGroup, status?: BookingStatus) =>
      c.get<BookingDto[]>('/admin/bookings', { params: { group, status } }).then(r => r.data),
    forceCancel: (id: string) =>
      c.post<BookingDto>(`/admin/bookings/${id}/force-cancel`).then(r => r.data),

    students: {
      list: (search?: string, status?: string, skip = 0, take = 100) =>
        c.get<StudentDto[]>('/admin/students', { params: { search, status, skip, take } }).then(r => r.data),
      upload: (file: File) => {
        const form = new FormData();
        form.append('file', file);
        return c.post<StudentImportResultDto>('/admin/students/upload', form).then(r => r.data);
      },
      downloadSample: async () => {
        const resp = await c.get('/admin/students/sample', { responseType: 'blob' });
        downloadBlob(resp.data as Blob, 'kfs-students-template.xlsx');
      },
      setActive: (id: string, isActive: boolean) =>
        c.patch<StudentDto>(`/admin/students/${id}`, { isActive }).then(r => r.data),
      resetPassword: (id: string) =>
        c.post<ResetPasswordResponseDto>(`/admin/students/${id}/reset-password`).then(r => r.data),
      deleteAll: () =>
        c.delete<{ deleted: number }>('/admin/students').then(r => r.data),
      sendWelcomeEmails: () =>
        c.post<{ total: number; queued: number }>('/admin/students/send-welcome-emails').then(r => r.data)
    },

    passes: {
      generate: (req: GeneratePassesRequest) =>
        c.post<GeneratePassesResponse>('/admin/passes/generate', req).then(r => r.data),
      quota: () =>
        c.get<PassQuotaDto[]>('/admin/passes/quota').then(r => r.data),
      setQuota: (type: AdminPassType, capacity: number) =>
        c.put<PassQuotaDto>('/admin/passes/quota', { type, capacity }).then(r => r.data),
      batches: () =>
        c.get<PassBatchSummaryDto[]>('/admin/passes/batches').then(r => r.data),
      list: (batchId?: string) =>
        c.get<AdminPassDto[]>('/admin/passes', { params: { batchId } }).then(r => r.data),
      setIssuedTo: (id: string, issuedToName: string) =>
        c.patch<AdminPassDto>(`/admin/passes/${id}`, { issuedToName }).then(r => r.data),
      download: async (batchId: string, format: PassOutputFormat, type: AdminPassType) => {
        const resp = await c.get(`/admin/passes/batches/${batchId}/download`, {
          params: { format }, responseType: 'blob'
        });
        const ext = format === 1 ? 'zip' : 'pdf';
        downloadBlob(resp.data as Blob, `passes-${AdminPassTypeName(type)}-${batchId.slice(0, 8)}.${ext}`);
      },
      deleteBatch: (batchId: string) =>
        c.delete<{ batchId: string; deleted: number }>(`/admin/passes/batches/${batchId}`).then(r => r.data),
      deleteAll: (type?: AdminPassType) =>
        c.delete<{ type: AdminPassType | null; deleted: number }>('/admin/passes/batches', { params: { type } }).then(r => r.data)
    },

    reports: {
      download: async (group: 'A' | 'B', format: 'csv' | 'xlsx' | 'pdf') => {
        const resp = await c.get(`/admin/reports/group/${group}`, {
          params: { format }, responseType: 'blob'
        });
        downloadBlob(resp.data as Blob, `group-${group}.${format}`);
      }
    },

    reminders: {
      sendUnbooked: (customSubject?: string, customBody?: string) =>
        c.post<{ sent: number } | unknown>('/admin/reminders/unbooked', { customSubject, customBody }).then(r => r.data),
      logs: (take = 100) =>
        c.get<ReminderLogDto[]>('/admin/reminders/logs', { params: { take } }).then(r => r.data)
    },

    event: {
      get: () => c.get<EventDto>('/admin/event').then(r => r.data),
      update: (eventId: string, req: UpdateEventRequest) =>
        c.put<EventDto>(`/admin/event/${eventId}`, req).then(r => r.data)
    },

    guest: {
      analytics: () => c.get<GuestAnalyticsDto>('/admin/guest/analytics').then(r => r.data),
      students: (search?: string) =>
        c.get<GuestEligibleStudentDto[]>('/admin/guest/students', { params: { search } }).then(r => r.data),
      issue: (studentId: string, issuedToName?: string) =>
        c.post<GuestPassDto>('/admin/guest/issue', { studentId, issuedToName }).then(r => r.data)
    },

    scans: (search?: string, status?: 'scanned' | 'unscanned', kind?: string) =>
      c.get<ScanAuditDto>('/admin/scans', { params: { search, status, kind } }).then(r => r.data)
  })
};

function AdminPassTypeName(t: AdminPassType): string {
  return (['vvip', 'guest', 'staff', 'media'] as const)[t] ?? 'pass';
}
