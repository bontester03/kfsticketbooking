import { Component, Input } from '@angular/core';

@Component({
  selector: 'kfs-empty-state',
  standalone: true,
  template: `
    <div class="empty">
      <h3>{{ title }}</h3>
      <p class="muted">{{ message }}</p>
      <ng-content></ng-content>
    </div>
  `,
  styles: [`
    .empty {
      text-align: center;
      padding: 2.5rem 1.5rem;
      border: 1px dashed var(--border);
      border-radius: 10px;
      background: var(--surface);
    }
    h3 { margin: 0 0 0.4rem; }
    p { margin: 0 0 1rem; }
  `]
})
export class EmptyStateComponent {
  @Input() title = 'Nothing here yet';
  @Input() message = '';
}
