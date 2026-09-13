import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { permissionGuard } from './core/guards/permission.guard';

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
        loadComponent: () => import('./features/dashboard/dashboard').then(module => module.Dashboard)
      },
      { path: 'products', canActivate: [permissionGuard], data: { permission: 'products.view' }, loadComponent: () => import('./features/products/products').then(module => module.Products) },
      { path: 'categories', canActivate: [permissionGuard], data: { permission: 'categories.view' }, loadComponent: () => import('./features/categories/categories').then(module => module.Categories) },
      { path: 'inventory', canActivate: [permissionGuard], data: { permission: 'stock.view' }, loadComponent: () => import('./features/inventory/inventory-list/inventory-list').then(module => module.InventoryList) },
      { path: 'pos', canActivate: [permissionGuard], data: { permission: 'sales.create' }, loadComponent: () => import('./features/pos/pos').then(module => module.Pos) },
      { path: 'sales', canActivate: [permissionGuard], data: { permission: 'sales.view' }, loadComponent: () => import('./features/sales/sales').then(module => module.Sales) },
      { path: 'customers', canActivate: [permissionGuard], data: { permission: 'customers.view' }, loadComponent: () => import('./features/customers/customers').then(module => module.Customers) },
      { path: 'users', canActivate: [permissionGuard], data: { permission: 'administration.users.view' }, loadComponent: () => import('./features/users/users').then(module => module.Users) },
      { path: 'roles', canActivate: [permissionGuard], data: { permission: 'administration.roles.view' }, loadComponent: () => import('./features/roles/roles').then(module => module.Roles) },
      { path: 'audit', canActivate: [permissionGuard], data: { permission: 'audit.view' }, loadComponent: () => import('./features/audit/audit').then(module => module.Audit) },
      { path: 'coming-soon', loadComponent: () => import('./features/coming-soon/coming-soon').then(module => module.ComingSoon) }
    ]
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' }
];
