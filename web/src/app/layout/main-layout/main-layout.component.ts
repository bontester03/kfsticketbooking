import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from '../header/header.component';

@Component({
  selector: 'kfs-main-layout',
  standalone: true,
  imports: [RouterOutlet, HeaderComponent],
  template: `
    <kfs-header />
    <main class="content">
      <router-outlet />
    </main>
  `,
  styles: [`
    .content { max-width: 1100px; margin: 1.5rem auto; padding: 0 1.25rem; }
  `]
})
export class MainLayoutComponent {}
