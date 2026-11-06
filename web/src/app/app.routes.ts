import { Routes } from '@angular/router';
import { authGuard } from './core/auth';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login-page').then(m => m.LoginPage) },
  { path: '', canActivate: [authGuard], loadComponent: () => import('./pages/list/list-page').then(m => m.ListPage) },
  { path: 'tilbud', canActivate: [authGuard], loadComponent: () => import('./pages/offers/offers-page').then(m => m.OffersPage) },
  { path: 'tur', canActivate: [authGuard], loadComponent: () => import('./pages/trip/trip-page').then(m => m.TripPage) },
  { path: '**', redirectTo: '' },
];
