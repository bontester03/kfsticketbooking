import { Routes } from '@angular/router';
import { authGuard } from '@core/guards/auth.guard';
import { MainLayoutComponent } from '@layout/main-layout/main-layout.component';

export const routes: Routes = [
  {
    path: 'auth',
    loadChildren: () => import('@features/auth/auth.routes').then(m => m.AUTH_ROUTES)
  },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('@features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'auditoriums',
        loadChildren: () => import('@features/auditoriums/auditoriums.routes').then(m => m.AUDITORIUMS_ROUTES)
      },
      {
        path: 'bookings',
        loadChildren: () => import('@features/bookings/bookings.routes').then(m => m.BOOKINGS_ROUTES)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
