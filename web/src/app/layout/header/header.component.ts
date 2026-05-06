import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'kfs-header',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <header class="topbar">
      <a routerLink="/" class="brand">KFS Booking</a>
      @if (auth.isAuthenticated()) {
        <nav class="links">
          <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Dashboard</a>
          <a routerLink="/auditoriums" routerLinkActive="active">Auditoriums</a>
          <a routerLink="/bookings" routerLinkActive="active">Bookings</a>
        </nav>
        <div class="user">
          <span class="muted">{{ auth.user()?.fullName }} ({{ auth.user()?.role }})</span>
          <button class="ghost" (click)="logout()">Sign out</button>
        </div>
      }
    </header>
  `,
  styles: [`
    .topbar {
      display: flex;
      align-items: center;
      gap: 1.5rem;
      padding: 0.85rem 1.5rem;
      background: var(--surface);
      border-bottom: 1px solid var(--border);
      position: sticky;
      top: 0;
      z-index: 10;
    }
    .brand { font-weight: 700; font-size: 1.1rem; color: var(--primary); }
    .links { display: flex; gap: 1rem; flex: 1; }
    .links a { color: var(--text); padding: 0.4rem 0.6rem; border-radius: 6px; }
    .links a.active { background: #eff6ff; color: var(--primary); }
    .user { display: flex; align-items: center; gap: 0.75rem; }
  `]
})
export class HeaderComponent {
  auth = inject(AuthService);
  private router = inject(Router);

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/auth/login']);
  }
}
