import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';

export const AUDITORIUMS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./auditoriums-list/auditoriums-list.component').then(m => m.AuditoriumsListComponent)
  },
  {
    path: 'new',
    canActivate: [authGuard],
    data: { roles: ['Admin'] },
    loadComponent: () => import('./auditorium-form/auditorium-form.component').then(m => m.AuditoriumFormComponent)
  },
  {
    path: ':id/edit',
    canActivate: [authGuard],
    data: { roles: ['Admin'] },
    loadComponent: () => import('./auditorium-form/auditorium-form.component').then(m => m.AuditoriumFormComponent)
  }
];
