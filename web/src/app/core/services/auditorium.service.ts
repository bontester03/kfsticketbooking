import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';
import { Auditorium, CreateAuditoriumRequest, UpdateAuditoriumRequest } from '../models/auditorium.model';

@Injectable({ providedIn: 'root' })
export class AuditoriumService {
  private http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/auditoriums`;

  list(): Observable<Auditorium[]> {
    return this.http.get<Auditorium[]>(this.base);
  }

  get(id: string): Observable<Auditorium> {
    return this.http.get<Auditorium>(`${this.base}/${id}`);
  }

  create(request: CreateAuditoriumRequest): Observable<Auditorium> {
    return this.http.post<Auditorium>(this.base, request);
  }

  update(id: string, request: UpdateAuditoriumRequest): Observable<Auditorium> {
    return this.http.put<Auditorium>(`${this.base}/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
