import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuditoriumService } from '@core/services/auditorium.service';
import { AuthService } from '@core/services/auth.service';
import { Auditorium } from '@core/models/auditorium.model';
import { SpinnerComponent } from '@shared/components/spinner/spinner.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';

@Component({
  selector: 'kfs-auditoriums-list',
  standalone: true,
  imports: [RouterLink, SpinnerComponent, EmptyStateComponent],
  template: `
    <header class="page-header">
      <div>
        <h1>Auditoriums</h1>
        <p class="muted">Browse halls available for booking.</p>
      </div>
      @if (canManage()) {
        <a routerLink="/auditoriums/new"><button>+ New auditorium</button></a>
      }
    </header>

    @if (loading()) {
      <kfs-spinner />
    } @else if (auditoriums().length === 0) {
      <kfs-empty-state title="No auditoriums" message="Once an admin adds halls, they'll appear here." />
    } @else {
      <div class="grid cards">
        @for (a of auditoriums(); track a.id) {
          <article class="card">
            <h3>{{ a.name }}</h3>
            <p class="muted">{{ a.location }}</p>
            <p>Capacity: <strong>{{ a.capacity }}</strong></p>
            @if (a.description) { <p>{{ a.description }}</p> }
            <div class="row" style="margin-top: 1rem;">
              <a [routerLink]="['/bookings/new']" [queryParams]="{ auditoriumId: a.id }">
                <button>Book this</button>
              </a>
              @if (canManage()) {
                <a [routerLink]="['/auditoriums', a.id, 'edit']">
                  <button class="secondary">Edit</button>
                </a>
              }
            </div>
          </article>
        }
      </div>
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
    h1 { margin: 0; }
    .cards { grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); }
  `]
})
export class AuditoriumsListComponent implements OnInit {
  private service = inject(AuditoriumService);
  private auth = inject(AuthService);

  auditoriums = signal<Auditorium[]>([]);
  loading = signal(true);
  canManage = computed(() => this.auth.hasRole('Admin'));

  ngOnInit(): void {
    this.service.list().subscribe({
      next: list => { this.auditoriums.set(list); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }
}
