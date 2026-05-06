// Types mirror the .NET API DTOs in api/src/KFS.Application/DTOs/. Manually maintained
// for now; once the backend exposes a stable OpenAPI doc the spec calls for codegen here
// (orval / openapi-typescript-codegen). Captured as a follow-up in DECISIONS.md.

export type UserType = 0 | 1; // 0 Student, 1 Admin
export type ZoneGroup = 0 | 1 | 2; // None, A, B
export type ZoneSide = 0 | 1 | 2;  // None, Female, Male
export type ZoneCode = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7;
export type SeatStatus = 0 | 1 | 2; // Available, Held, Booked
export type ParentRole = 0 | 1; // Mother, Father
export type BookingStatus = 0 | 1 | 2 | 3 | 4; // Cart, Confirmed, Cancelled, Expired, RebookWindow
export type AdminPassType = 0 | 1 | 2 | 3; // VVIP, Guest, Staff, Media

export const ZoneGroup = { None: 0, A: 1, B: 2 } as const;
export const ZoneSide = { None: 0, Female: 1, Male: 2 } as const;
export const SeatStatus = { Available: 0, Held: 1, Booked: 2 } as const;
export const BookingStatus = {
  Cart: 0, Confirmed: 1, Cancelled: 2, Expired: 3, RebookWindow: 4
} as const;
export const ParentRole = { Mother: 0, Father: 1 } as const;

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
}

export interface EventDto {
  id: string;
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
