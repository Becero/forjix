import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { CreateInventoryMovementRequest, InventoryItem, InventoryMovementItem, InventoryMovementResult, PagedResult } from './inventory.models';

@Injectable({ providedIn: 'root' })
export class InventoryApiService {
  private readonly http = inject(HttpClient);
  list(filters: Record<string, string>) { let params = new HttpParams(); Object.entries(filters).filter(([, value]) => value).forEach(([key, value]) => params = params.set(key, value)); return this.http.get<InventoryItem[]>('/api/inventory', { params }); }
  movements(productId: string, filters: Record<string, string | number>) { let params = new HttpParams(); Object.entries(filters).filter(([, value]) => value !== '').forEach(([key, value]) => params = params.set(key, value)); return this.http.get<PagedResult<InventoryMovementItem>>(`/api/inventory/${productId}/movements`, { params }); }
  createMovement(productId: string, request: CreateInventoryMovementRequest) { return this.http.post<InventoryMovementResult>(`/api/inventory/${productId}/movements`, request); }
}
