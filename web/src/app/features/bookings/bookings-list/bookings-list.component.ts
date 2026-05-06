import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { BookingService } from '@core/services/booking.service';
import { AuthService } from '@core/services/auth.service';
import { Booking, BookingStatus } from '@core/models/booking.model';
import { SpinnerComponent } from '@shared/components/spinner/spinner.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';

@Component({
  selector: 'kfs-bookings-list',
  standalone: true,
  imports: [DatePipe, RouterLink, SpinnerComponent, EmptyStateComponent, StatusBadgeComponent],
  template: `
    <header class="page-header">
      <div>
        <h1>Bookings</h1>
        <p class="muted">{{ scopeLabel() }}</p>
      </div>
      <div class="row">
        @if (canSeeAll()) {
          <button class="secondary" (click)="setScope('all')" [disabled]="scope() === 'all'">All</button>
          <button class="secondary" (click)="setScope('mine')" [disabled]="scope() === 'mine'">Mine</button>
        }
        <a routerLink="/bookings/new"><button>+ New booking</button></a>
      </div>
    </header>

    @if (loading()) {
      <kfs-spinner />
    } @else if (bookings().length === 0) {
      <kfs-empty-state title="No bookings yet" message="Create your first booking to get started.">
        <a routerLink="/bookings/new"><button>Create booking</button></a>
      </kfs-empty-state>
    } @else {
      <table class="table">
        <thead>
          <tr>
            <th>Auditorium</th>
            <th>Booked by</th>
            <th>Purpose</th>
            <th>Start</th>
            <th>End</th>
            <th>Status</th>
            @if (canModerate()) { <th>Actions</th> }
          </tr>
        </thead>
        <tbody>
          @for (b of bookings(); track b.id) {
            <tr>
              <td>{{ b.auditoriumName }}</td>
              <td>{{ b.userName }}</td>
              <td>{{ b.purpose }}</td>
              <td>{{ b.startTime | date:'medium' }}</td>
              <td>{{ b.endTime | date:'medium' }}</td>
              <td><kfs-status-badge [status]="b.status" /></td>
              @if (canModerate()) {
                <td class="row">
                  @if (b.status === 'Pending') {
                    <button class="ghost" (click)="setStatus(b, 'Approved')">Approve</button>
                    <button class="danger" (click)="setStatus(b, 'Rejected')">Reject</button>
                  }
                  @if (b.status === 'Pending' || b.status === 'Approved') {
                    <button class="secondary" (click)="cancel(b)">Cancel</button>
                  }
                </td>
              }
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; flex-wrap: wrap; gap: 1rem; }
    h1 { margin: 0; }
    .table { width: 100%; border-collapse: collapse; background: var(--surface); border: 1px solid var(--border); border-radius: 10px; overflow: hidden; }
    .table th, .table td { padding: 0.7rem 0.9rem; text-align: left; border-bottom: 1px solid var(--border); font-size: 0.92rem; }
    .table thead { background: #f1f5f9; }
    .table tbody tr:last-child td { border-bottom: none; }
  `]
})
export class BookingsListComponent implements OnInit {
  private service = inject(BookingService);
  private auth = inject(AuthService);

  bookings = signal<Booking[]>([]);
  loading = signal(true);
  scope = signal<'all' | 'mine'>('mine');

  canSeeAll = computed(() => this.auth.hasRole('Admin', 'Teacher'));
  canModerate = computed(() => this.auth.hasRole('Admin'));
  scopeLabel = computed(() => this.scope() === 'all' ? 'All bookings' : 'Your bookings');

  ngOnInit(): void {
    this.scope.set(this.canSeeAll() ? 'all' : 'mine');
    this.refresh();
  }

  setScope(scope: 'all' | 'mine'): void {
    this.scope.set(scope);
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    const obs = this.scope() === 'all' ? this.service.listAll() : this.service.listMine();
    obs.subscribe({
      next: list => { this.bookings.set(list); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  setStatus(b: Booking, status: BookingStatus): void {
    const reason = status === 'Rejected' ? prompt('Reason for rejection?') ?? '' : null;
    this.service.updateStatus(b.id, { status, rejectionReason: reason }).subscribe(() => this.refresh());
  }

  cancel(b: Booking): void {
    if (!confirm('Cancel this booking?')) return;
    this.service.cancel(b.id).subscribe(() => this.refresh());
  }
}
