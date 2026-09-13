import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AuditItem, CategoryItem, PermissionItem, ProductItem, RoleItem, SaveCategoryRequest, SaveProductRequest, SaveRoleRequest, SaveUserRequest, UserItem } from './management.models';

@Injectable({ providedIn: 'root' })
export class ManagementApiService {
  private readonly http = inject(HttpClient);

  users() { return this.http.get<UserItem[]>('/api/users'); }
  createUser(request: SaveUserRequest) { return this.http.post<UserItem>('/api/users', request); }
  updateUser(id: string, request: SaveUserRequest) { return this.http.put<UserItem>(`/api/users/${id}`, request); }
  roles() { return this.http.get<RoleItem[]>('/api/roles'); }
  permissions() { return this.http.get<PermissionItem[]>('/api/roles/permissions'); }
  createRole(request: SaveRoleRequest) { return this.http.post<RoleItem>('/api/roles', request); }
  updateRole(id: string, request: SaveRoleRequest) { return this.http.put<RoleItem>(`/api/roles/${id}`, request); }
  audit(filters: Record<string, string>) { let params = new HttpParams(); Object.entries(filters).filter(([, value]) => value).forEach(([key, value]) => params = params.set(key, value)); return this.http.get<AuditItem[]>('/api/audit', { params }); }
  categories(includeInactive = true) { return this.http.get<CategoryItem[]>('/api/categories', { params: { includeInactive } }); }
  createCategory(request: SaveCategoryRequest) { return this.http.post<CategoryItem>('/api/categories', request); }
  updateCategory(id: string, request: SaveCategoryRequest) { return this.http.put<CategoryItem>(`/api/categories/${id}`, request); }
  deactivateCategory(id: string) { return this.http.delete<CategoryItem>(`/api/categories/${id}`); }
  products(filters: { search?: string; categoryId?: string; isActive?: string }) { let params = new HttpParams(); Object.entries(filters).filter(([, value]) => value !== undefined && value !== '').forEach(([key, value]) => params = params.set(key, value!)); return this.http.get<ProductItem[]>('/api/products', { params }); }
  createProduct(request: SaveProductRequest) { return this.http.post<ProductItem>('/api/products', request); }
  updateProduct(id: string, request: SaveProductRequest) { return this.http.put<ProductItem>(`/api/products/${id}`, request); }
  deactivateProduct(id: string) { return this.http.delete<ProductItem>(`/api/products/${id}`); }
}
