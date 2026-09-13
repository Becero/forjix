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
        path: '', canActivate: [permissionGuard], data: { permission: 'dashboard.view' },
        loadComponent: () => import('./features/dashboard/dashboard').then(module => module.Dashboard)
      },
      { path: 'products', canActivate: [permissionGuard], data: { permission: 'products.view' }, loadComponent: () => import('./features/products/products').then(module => module.Products) },
      { path: 'categories', canActivate: [permissionGuard], data: { permission: 'categories.view' }, loadComponent: () => import('./features/categories/categories').then(module => module.Categories) },
      { path: 'inventory', canActivate: [permissionGuard], data: { permission: 'stock.view' }, loadComponent: () => import('./features/inventory/inventory-list/inventory-list').then(module => module.InventoryList) },
      { path: 'pos', canActivate: [permissionGuard], data: { permission: 'sales.create' }, loadComponent: () => import('./features/pos/pos').then(module => module.Pos) },
      { path: 'sales', canActivate: [permissionGuard], data: { permission: 'sales.view' }, loadComponent: () => import('./features/sales/sales').then(module => module.Sales) },
      { path: 'customers', canActivate: [permissionGuard], data: { permission: 'customers.view' }, loadComponent: () => import('./features/customers/customers').then(module => module.Customers) },
      { path: 'suppliers', canActivate: [permissionGuard], data: { permission: 'suppliers.view' }, loadComponent: () => import('./features/suppliers/suppliers').then(module => module.Suppliers) },
      { path: 'purchases', canActivate: [permissionGuard], data: { permission: 'purchases.view' }, loadComponent: () => import('./features/purchases/purchases').then(module => module.Purchases) },
      { path: 'cash', canActivate: [permissionGuard], data: { permission: 'cash.view' }, loadComponent: () => import('./features/cash/cash').then(module => module.Cash) },
      { path: 'reports', canActivate: [permissionGuard], data: { permission: 'reports.view' }, loadComponent: () => import('./features/reports/reports').then(module => module.Reports) },
      { path: 'settings', canActivate: [permissionGuard], data: { permission: 'settings.view' }, loadComponent: () => import('./features/settings/settings').then(module => module.Settings) },
      { path: 'users', canActivate: [permissionGuard], data: { permission: 'administration.users.view' }, loadComponent: () => import('./features/users/users').then(module => module.Users) },
      { path: 'roles', canActivate: [permissionGuard], data: { permission: 'administration.roles.view' }, loadComponent: () => import('./features/roles/roles').then(module => module.Roles) },
      { path: 'audit', canActivate: [permissionGuard], data: { permission: 'audit.view' }, loadComponent: () => import('./features/audit/audit').then(module => module.Audit) }
    ]
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' }
];
