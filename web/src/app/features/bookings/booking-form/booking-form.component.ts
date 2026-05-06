import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AuditoriumService } from '@core/services/auditorium.service';
import { BookingService } from '@core/services/booking.service';
import { Auditorium } from '@core/models/auditorium.model';

@Component({
  selector: 'kfs-booking-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <section class="container">
      <h1>New booking</h1>
      <form class="card" [formGroup]="form" (ngSubmit)="submit()">
        <div class="field">
          <label>Auditorium</label>
          <select formControlName="auditoriumId">
            <option value="">Select an auditorium</option>
            @for (a of auditoriums(); track a.id) {
              <option [value]="a.id">{{ a.name }} (cap. {{ a.capacity }})</option>
            }
          </select>
        </div>
        <div class="field">
          <label>Purpose</label>
          <input type="text" formControlName="purpose" maxlength="500" />
        </div>
        <div class="field">
          <label>Start</label>
          <input type="datetime-local" formControlName="startTime" />
        </div>
        <div class="field">
          <label>End</label>
          <input type="datetime-local" formControlName="endTime" />
        </div>
        @if (error()) { <p class="error">{{ error() }}</p> }
        <div class="row">
          <button type="submit" [disabled]="form.invalid || loading()">
            {{ loading() ? 'Submitting...' : 'Request booking' }}
          </button>
          <button type="button" class="secondary" (click)="cancel()">Cancel</button>
        </div>
      </form>
    </section>
  `,
  styles: [`.container { max-width: 540px; margin: 1.5rem auto; padding: 0 1rem; }`]
})
export class BookingFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private auditoriumService = inject(AuditoriumService);
  private bookingService = inject(BookingService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  auditoriums = signal<Auditorium[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);

  form = this.fb.nonNullable.group({
    auditoriumId: ['', Validators.required],
    purpose: ['', [Validators.required, Validators.maxLength(500)]],
    startTime: ['', Validators.required],
    endTime: ['', Validators.required]
  });

  ngOnInit(): void {
    this.auditoriumService.list().subscribe(list => {
      this.auditoriums.set(list.filter(a => a.isActive));
      const preselect = this.route.snapshot.queryParamMap.get('auditoriumId');
      if (preselect) this.form.patchValue({ auditoriumId: preselect });
    });
  }

  submit(): void {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    const v = this.form.getRawValue();
    this.bookingService.create({
      auditoriumId: v.auditoriumId,
      purpose: v.purpose,
      startTime: new Date(v.startTime).toISOString(),
      endTime: new Date(v.endTime).toISOString()
    }).subscribe({
      next: () => this.router.navigate(['/bookings']),
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(err.error?.message ?? 'Could not create booking.');
      }
    });
  }

  cancel(): void { this.router.navigate(['/bookings']); }
}
