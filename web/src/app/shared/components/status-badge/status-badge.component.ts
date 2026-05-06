import { Component, Input } from '@angular/core';
import { BookingStatus } from '@core/models/booking.model';

@Component({
  selector: 'kfs-status-badge',
  standalone: true,
  template: `<span class="badge" [class]="'badge--' + status.toLowerCase()">{{ status }}</span>`,
  styles: [`
    .badge {
      display: inline-block;
      font-size: 0.78rem;
      font-weight: 600;
      padding: 0.2rem 0.55rem;
      border-radius: 999px;
      background: #e2e8f0;
      color: #1e293b;
    }
    .badge--pending { background: #fef3c7; color: #92400e; }
    .badge--approved { background: #dcfce7; color: #166534; }
    .badge--rejected { background: #fee2e2; color: #991b1b; }
    .badge--cancelled { background: #e2e8f0; color: #475569; }
    .badge--completed { background: #dbeafe; color: #1e40af; }
  `]
})
export class StatusBadgeComponent {
  @Input({ required: true }) status!: BookingStatus;
}
