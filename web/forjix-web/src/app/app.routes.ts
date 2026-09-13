import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./core/auth/login/login').then((module) => module.Login)
  },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () => import('./layouts/authenticated-layout/authenticated-layout').then((module) => module.AuthenticatedLayout),
    children: [
      {
        path: '',
        loadComponent: () => import('./features/workspace-placeholder/workspace-placeholder').then((module) => module.WorkspacePlaceholder)
      }
    ]
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' }
];
