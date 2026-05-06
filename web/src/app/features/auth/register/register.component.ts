import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'kfs-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth">
      <div class="card">
        <h2>Create account</h2>
        <p class="muted">Register to book auditoriums.</p>
        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="field">
            <label>Full name</label>
            <input type="text" formControlName="fullName" />
          </div>
          <div class="field">
            <label>Email</label>
            <input type="email" formControlName="email" autocomplete="email" />
          </div>
          <div class="field">
            <label>Password</label>
            <input type="password" formControlName="password" autocomplete="new-password" />
          </div>
          @if (error()) { <p class="error">{{ error() }}</p> }
          <button type="submit" [disabled]="form.invalid || loading()">
            {{ loading() ? 'Creating...' : 'Create account' }}
          </button>
        </form>
        <p class="muted" style="margin-top: 1rem;">
          Already have an account? <a routerLink="/auth/login">Sign in</a>
        </p>
      </div>
    </section>
  `,
  styles: [`
    .auth { max-width: 420px; margin: 3rem auto; padding: 0 1rem; }
    h2 { margin: 0 0 0.25rem; }
  `]
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  loading = signal(false);
  error = signal<string | null>(null);

  form = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(120)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  submit(): void {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    this.auth.register(this.form.getRawValue()).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/']);
      },
      error: (err: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(err.error?.message ?? 'Registration failed.');
      }
    });
  }
}
