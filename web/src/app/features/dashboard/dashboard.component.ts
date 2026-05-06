import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'kfs-dashboard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="hero card">
      <h1>Welcome, {{ user()?.fullName }}</h1>
      <p class="muted">KFS School auditorium booking — fast, simple, audit-ready.</p>
      <div class="row">
        <a routerLink="/auditoriums"><button>Browse auditoriums</button></a>
        <a routerLink="/bookings"><button class="secondary">My bookings</button></a>
        <a routerLink="/bookings/new"><button class="secondary">New booking</button></a>
      </div>
    </section>

    <section class="grid stats">
      <div class="card stat">
        <span class="muted">Your role</span>
        <strong>{{ user()?.role }}</strong>
      </div>
      <div class="card stat">
        <span class="muted">Email</span>
        <strong>{{ user()?.email }}</strong>
      </div>
      <div class="card stat">
        <span class="muted">Quick action</span>
        @if (isAdmin()) {
          <a routerLink="/auditoriums/new">Add a new auditorium</a>
        } @else {
          <a routerLink="/bookings/new">Request a new booking</a>
        }
      </div>
    </section>
  `,
  styles: [`
    .hero { margin-bottom: 1.5rem; }
    .hero h1 { margin: 0 0 0.5rem; }
    .stats { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
    .stat strong { display: block; margin-top: 0.25rem; font-size: 1.1rem; }
  `]
})
export class DashboardComponent {
  private auth = inject(AuthService);
  user = this.auth.user;
  isAdmin = computed(() => this.auth.hasRole('Admin'));
}
