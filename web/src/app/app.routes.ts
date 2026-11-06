import { Routes } from '@angular/router';
import { authGuard } from './core/auth';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login-page').then(m => m.LoginPage) },
  { path: '', canActivate: [authGuard], loadComponent: () => import('./pages/list/list-page').then(m => m.ListPage) },
  { path: 'tilbud', canActivate: [authGuard], loadComponent: () => import('./pages/offers/offers-page').then(m => m.OffersPage) },
  { path: 'madplan', canActivate: [authGuard], loadComponent: () => import('./pages/meals/meals-page').then(m => m.MealsPage) },
  { path: 'tur', canActivate: [authGuard], loadComponent: () => import('./pages/trip/trip-page').then(m => m.TripPage) },
  { path: 'tur/arkiv', canActivate: [authGuard], loadComponent: () => import('./pages/trip/trips-archive-page').then(m => m.TripsArchivePage) },
  { path: 'tur/:id', canActivate: [authGuard], loadComponent: () => import('./pages/trip/trip-detail-page').then(m => m.TripDetailPage) },
  { path: '**', redirectTo: '' },
];
