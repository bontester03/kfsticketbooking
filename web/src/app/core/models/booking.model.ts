export type BookingStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled' | 'Completed';

export interface Booking {
  id: string;
  userId: string;
  userName: string;
  auditoriumId: string;
  auditoriumName: string;
  purpose: string;
  startTime: string;
  endTime: string;
  status: BookingStatus;
  rejectionReason?: string | null;
  createdAt: string;
}

export interface CreateBookingRequest {
  auditoriumId: string;
  purpose: string;
  startTime: string;
  endTime: string;
}

export interface UpdateBookingStatusRequest {
  status: BookingStatus;
  rejectionReason?: string | null;
}
