import { Component } from '@angular/core';

@Component({
  selector: 'kfs-spinner',
  standalone: true,
  template: `<div class="spinner" role="status" aria-label="Loading"></div>`,
  styles: [`
    .spinner {
      width: 28px;
      height: 28px;
      border: 3px solid #e2e8f0;
      border-top-color: var(--primary);
      border-radius: 50%;
      animation: spin 0.7s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class SpinnerComponent {}
