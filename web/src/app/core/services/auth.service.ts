import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '@env/environment';
import { AuthResponse, CurrentUser, LoginRequest, RegisterRequest, UserRole } from '../models/user.model';

const STORAGE_KEY = 'kfs.auth';

interface StoredAuth {
  token: string;
  expiresAt: string;
  user: CurrentUser;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/auth`;

  private readonly _state = signal<StoredAuth | null>(this.readStorage());

  readonly user = computed<CurrentUser | null>(() => this._state()?.user ?? null);
  readonly token = computed<string | null>(() => this._state()?.token ?? null);
  readonly isAuthenticated = computed(() => !!this._state() && !this.isExpired(this._state()));
  readonly role = computed<UserRole | null>(() => this._state()?.user.role ?? null);

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/login`, request)
      .pipe(tap(resp => this.persist(resp)));
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/register`, request)
      .pipe(tap(resp => this.persist(resp)));
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this._state.set(null);
  }

  hasRole(...roles: UserRole[]): boolean {
    const r = this.role();
    return r != null && roles.includes(r);
  }

  private persist(resp: AuthResponse): void {
    const stored: StoredAuth = {
      token: resp.token,
      expiresAt: resp.expiresAt,
      user: {
        userId: resp.userId,
        email: resp.email,
        fullName: resp.fullName,
        role: resp.role
      }
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
    this._state.set(stored);
  }

  private readStorage(): StoredAuth | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw) as StoredAuth;
      if (this.isExpired(parsed)) {
        localStorage.removeItem(STORAGE_KEY);
        return null;
      }
      return parsed;
    } catch {
      return null;
    }
  }

  private isExpired(state: StoredAuth | null): boolean {
    if (!state) return true;
    return new Date(state.expiresAt).getTime() <= Date.now();
  }
}
