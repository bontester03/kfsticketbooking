import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AuditoriumService } from '@core/services/auditorium.service';

@Component({
  selector: 'kfs-auditorium-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <section class="container">
      <h1>{{ id() ? 'Edit auditorium' : 'New auditorium' }}</h1>
      <form class="card" [formGroup]="form" (ngSubmit)="submit()">
        <div class="field">
          <label>Name</label>
          <input type="text" formControlName="name" />
        </div>
        <div class="field">
          <label>Location</label>
          <input type="text" formControlName="location" />
        </div>
        <div class="field">
          <label>Capacity</label>
          <input type="number" formControlName="capacity" min="1" />
        </div>
        <div class="field">
          <label>Description</label>
          <textarea rows="3" formControlName="description"></textarea>
        </div>
        @if (id()) {
          <div class="field row">
            <input type="checkbox" formControlName="isActive" id="isActive" style="width: auto;" />
            <label for="isActive" style="margin: 0;">Active</label>
          </div>
        }
        @if (error()) { <p class="error">{{ error() }}</p> }
        <div class="row">
          <button type="submit" [disabled]="form.invalid || loading()">
            {{ loading() ? 'Saving...' : 'Save' }}
          </button>
          <button type="button" class="secondary" (click)="cancel()">Cancel</button>
        </div>
      </form>
    </section>
  `,
  styles: [`.container { max-width: 540px; margin: 1.5rem auto; padding: 0 1rem; }`]
})
export class AuditoriumFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(AuditoriumService);

  id = signal<string | null>(null);
  loading = signal(false);
  error = signal<string | null>(null);

  form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    location: ['', [Validators.required, Validators.maxLength(180)]],
    capacity: [50, [Validators.required, Validators.min(1)]],
    description: [''],
    isActive: [true]
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.id.set(id);
      this.service.get(id).subscribe(a => this.form.patchValue(a));
    }
  }

  submit(): void {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    const id = this.id();
    const obs = id
      ? this.service.update(id, value)
      : this.service.create({ name: value.name, location: value.location, capacity: value.capacity, description: value.description });

    obs.subscribe({
      next: () => this.router.navigate(['/auditoriums']),
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(err.error?.message ?? 'Save failed.');
      }
    });
  }

  cancel(): void { this.router.navigate(['/auditoriums']); }
}
