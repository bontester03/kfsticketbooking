import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { Booking, CreateBookingRequest, UpdateBookingStatusRequest } from '../models/booking.model';

@Injectable({ providedIn: 'root' })
export class BookingService {
  private http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/bookings`;

  listAll(): Observable<Booking[]> {
    return this.http.get<Booking[]>(this.base);
  }

  listMine(): Observable<Booking[]> {
    return this.http.get<Booking[]>(`${this.base}/mine`);
  }

  get(id: string): Observable<Booking> {
    return this.http.get<Booking>(`${this.base}/${id}`);
  }

  create(request: CreateBookingRequest): Observable<Booking> {
    return this.http.post<Booking>(this.base, request);
  }

  updateStatus(id: string, request: UpdateBookingStatusRequest): Observable<Booking> {
    return this.http.patch<Booking>(`${this.base}/${id}/status`, request);
  }

  cancel(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/cancel`, {});
  }
}
