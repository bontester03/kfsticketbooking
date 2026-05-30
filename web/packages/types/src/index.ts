// Types mirror the .NET API DTOs in api/src/KFS.Application/DTOs/. Manually maintained
// for now; once the backend exposes a stable OpenAPI doc the spec calls for codegen here
// (orval / openapi-typescript-codegen). Captured as a follow-up in DECISIONS.md.

export type UserType = 0 | 1; // 0 Student, 1 Admin
export type ZoneGroup = 0 | 1 | 2; // None, A, B
export type ZoneSide = 0 | 1 | 2;  // None, Female, Male
export type ZoneCode = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13;
export type SeatStatus = 0 | 1 | 2; // Available, Held, Booked
export type ParentRole = 0 | 1 | 2; // Mother, Father, Grandmother (girls event)
export type BookingStatus = 0 | 1 | 2 | 3 | 4; // Cart, Confirmed, Cancelled, Expired, RebookWindow
export type AdminPassType = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7;
export type ZoneVisibility = 0 | 1 | 2; // PublicBookable, AdminOnly, DisplayOnly
export type EventGender = 1 | 2; // 1 Male/Boys, 2 Female/Girls

export const ZoneGroup = { None: 0, A: 1, B: 2 } as const;
export const ZoneSide = { None: 0, Female: 1, Male: 2 } as const;
export const SeatStatus = { Available: 0, Held: 1, Booked: 2 } as const;
export const BookingStatus = {
  Cart: 0, Confirmed: 1, Cancelled: 2, Expired: 3, RebookWindow: 4
} as const;
export const ParentRole = { Mother: 0, Father: 1, Grandmother: 2 } as const;
export const ZoneVisibility = { PublicBookable: 0, AdminOnly: 1, DisplayOnly: 2 } as const;
export const EventGender = { Male: 1, Female: 2 } as const;

export interface AuthResponse {
  accessToken: string;
  accessExpiresAt: string;
  refreshToken: string;
  refreshExpiresAt: string;
  userId: string;
  userType: UserType;
  email: string;
  displayName: string;
  mustChangePassword: boolean;
  /** Students only: 1 = VIP A, 2 = VIP B, null = not yet assigned. */
  assignedGroup?: number | null;
}

export interface EventDto {
  id: string;
  name: string;
  /** URL-safe identifier — "boys" or "girls". Used in admin routes /admin/{slug}/... */
  slug: string;
  gender: EventGender;
  /** "Father & Mother" (boys) / "Mother & Grandmother" (girls). */
  pairLabel: string;
  /** Seats covered by one student-issued Guest QR (boys=3, girls=5). */
  guestSeatsPerPass: number;
  eventDate: string;
  venue: string;
  venueAddress: string;
  mapLink?: string | null;
  isActive: boolean;
  bookingOpensAt: string;
  bookingClosesAt: string;
  cartHoldMinutes: number;
  cancellationWindowMinutes: number;
  reminderNoteFromAdmin?: string | null;
  scannerToken: string;
}

export interface SeatMapSeatDto {
  id: string;
  rowLabel: string;
  seatNumber: number;
  fullLabel: string;
  status: SeatStatus;
  occupantBookingId?: string | null;
  occupantName?: string | null;
}

export interface SeatMapZoneDto {
  zoneId: string;
  code: ZoneCode;
  side: ZoneSide;
  displayName: string;
  capacity: number;
  seats: SeatMapSeatDto[];
}

export interface SeatMapDto {
  group: ZoneGroup;
  femaleZone: SeatMapZoneDto;
  maleZone: SeatMapZoneDto;
}

export interface BookingItemDto {
  id: string;
  seatId: string;
  block: string;
  rowLabel: string;
  seatNumber: number;
  fullLabel: string;
  parentRole: ParentRole;
  ticketNumber: string;
  qrCodeImageUrl?: string | null;
  emailSent: boolean;
  holdExpiresAt: string;
  scanned: boolean;
  scannedAt?: string | null;
}

export interface BookingDto {
  id: string;
  studentId: string;
  status: BookingStatus;
  groupChosen: ZoneGroup;
  createdAt: string;
  confirmedAt?: string | null;
  cancelledAt?: string | null;
  rebookWindowExpiresAt?: string | null;
  items: BookingItemDto[];
}

export interface ApiError {
  status: number;
  code: string;
  message: string;
  extra?: unknown;
}

// ---- Admin DTOs (mirror api/src/KFS.Application/DTOs/*) -----------------------------------------

// `AdminPassType` type alias is declared near the top of this file; here we add the value object.
export const AdminPassType = {
  VVIP: 0, Guest: 1, Staff: 2, Media: 3,
  Photographer: 4, PersonalAssistant: 5, Visitor: 6, Emergency: 7
} as const;

export type PassOutputFormat = 0 | 1; // Pdf, Zip
export const PassOutputFormat = { Pdf: 0, Zip: 1 } as const;

export interface StudentDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  /** DOB is optional on the new roster format (StudentNumber replaces it for password generation). */
  dateOfBirth?: string | null;
  gradeOrClass?: string | null;
  isActive: boolean;
  mustChangePassword: boolean;
  bookingStatus?: string | null;
  createdAt: string;
  /** Booked VIP seats (only when status=Confirmed), e.g. "A12 & A11". */
  bookedSeats?: string | null;
  studentNumber?: string | null;
  preferredName?: string | null;
  gender?: string | null;
  /** 1 = VIP A, 2 = VIP B, null = not yet assigned. */
  assignedGroup?: number | null;
}

export interface StudentImportRowResultDto {
  rowNumber: number;
  imported: boolean;
  message?: string | null;
}

export interface StudentImportResultDto {
  totalRows: number;
  imported: number;
  skipped: number;
  failed: number;
  rowResults: StudentImportRowResultDto[];
}

export interface ResetPasswordResponseDto {
  generatedPassword: string;
}

export interface ZoneCapacityDto {
  zone: string;
  capacity: number;
  issued: number;
  percentIssued: number;
}

export interface DashboardStatsDto {
  studentsTotal: number;
  studentsLoggedIn: number;
  cartCount: number;
  confirmed: number;
  cancelled: number;
  scansToday: number;
  zones: ZoneCapacityDto[];
}

export interface GeneratePassesRequest {
  eventId: string;
  type: AdminPassType;
  count: number;
  format: PassOutputFormat;
}

export interface SetPassQuotaRequest {
  eventId: string;
  type: AdminPassType;
  capacity: number;
}

export interface GeneratePassesResponse {
  batchId: string;
  count: number;
  downloadUrl: string;
  format: PassOutputFormat;
}

export interface AdminPassDto {
  id: string;
  batchId: string;
  type: AdminPassType;
  sequenceNumber: number;
  ticketNumber: string;
  seatsCount: number;
  issuedToName?: string | null;
  qrCodeImageUrl?: string | null;
  issuedAt: string;
  admittedCount: number;
  /** "Gate A" / "Gate B" for student-linked Guest passes, otherwise null (use type default). */
  gate?: string | null;
  /** Roster-generated passes only — null for VVIP/Guest/Staff pool batches. */
  issuedToEmail?: string | null;
  /** Has the holder been emailed their QR yet? */
  emailSent: boolean;
  emailSentAt?: string | null;
}

export interface RosterPreviewRowDto {
  rowNumber: number;
  fullName: string;
  email: string;
  isDuplicate: boolean;
}

export interface RosterRowErrorDto {
  rowNumber: number;
  field: string;
  message: string;
}

export interface RosterPreviewDto {
  totalRows: number;
  wouldImport: number;
  wouldSkipDuplicates: number;
  errorRows: number;
  quotaCapacity: number;
  quotaIssued: number;
  quotaRemaining: number;
  rows: RosterPreviewRowDto[];
  errors: RosterRowErrorDto[];
}

export interface GenerateFromRosterResponse {
  batchId: string;
  rowsRead: number;
  generated: number;
  skipped: number;
  errors: RosterRowErrorDto[];
}

export interface SendBatchEmailsResponse {
  batchId: string;
  totalInBatch: number;
  sent: number;
  skipped: number;
  failed: number;
}

// ---- Guest tickets & scanning -----------------------------------------------------------------

export interface GuestPassDto {
  id: string;
  ticketNumber: string;
  seatsCount: number;
  admittedCount: number;
  fullyUsed: boolean;
  qrCodeImageUrl?: string | null;
  studentId?: string | null;
  studentName?: string | null;
  issuedByAdmin: boolean;
  issuedAt: string;
  /** "Gate A" or "Gate B" — derived from the child's VIP booking; "Gate A" default. */
  gate: string;
}

export interface GuestAnalyticsDto {
  limit: number;
  issued: number;
  remaining: number;
  passesTotal: number;
  bookedByStudents: number;
  issuedByAdminToChild: number;
  unassignedPool: number;
  admittedPeople: number;
}

export interface GuestEligibleStudentDto {
  id: string;
  fullName: string;
  email: string;
  hasGuestPass: boolean;
}

export type ScanResult = 0 | 1 | 2 | 3; // Valid, AlreadyUsed, Invalid, Expired
export const ScanResult = { Valid: 0, AlreadyUsed: 1, Invalid: 2, Expired: 3 } as const;

export interface ScanAuditRow {
  kind: string;
  ticketNumber: string;
  holder?: string | null;
  detail?: string | null;
  seatsCount: number;
  admittedCount: number;
  scanned: boolean;
  firstScannedAt?: string | null;
  lastScannedAt?: string | null;
}

export interface ScanAuditDto {
  totalTickets: number;
  scannedTickets: number;
  admittedPeople: number;
  rows: ScanAuditRow[];
}

export interface ScanResponse {
  valid: boolean;
  result: ScanResult;
  itemType?: number | null;
  zone?: string | null;
  seatLabel?: string | null;
  seatsCount?: number | null;
  admittedCount: number;
  holderName?: string | null;
  alreadyScanned: boolean;
  firstScannedAt?: string | null;
  message?: string | null;
}

export interface PassBatchSummaryDto {
  batchId: string;
  type: AdminPassType;
  count: number;
  seatsTotal: number;
  createdAt: string;
  pdfUrl?: string | null;
  zipUrl?: string | null;
  scannedPasses: number;
}

export interface PublicEventDto {
  name: string;
  slug: string;
  gender: EventGender;
  eventDate: string;
  venue: string;
  venueAddress: string;
  bookingOpensAt: string;
  bookingClosesAt: string;
  seatsTotal: number;
  seatsRemaining: number;
}

export interface PassQuotaDto {
  type: AdminPassType;
  label: string;
  capacity: number;
  issued: number;
  remaining: number;
}

export interface ReminderLogDto {
  id: string;
  type: string;
  studentId?: string | null;
  studentEmail?: string | null;
  sentAt: string;
  emailMessageId?: string | null;
}

export interface UpdateEventRequest {
  name: string;
  eventDate: string;
  venue: string;
  venueAddress: string;
  mapLink?: string | null;
  isActive: boolean;
  bookingOpensAt: string;
  bookingClosesAt: string;
  cartHoldMinutes: number;
  cancellationWindowMinutes: number;
  reminderNoteFromAdmin?: string | null;
}
