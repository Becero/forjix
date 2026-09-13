import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { CreateSaleRequest, PagedSales, Sale } from './sales.models';

@Injectable({ providedIn: 'root' })
export class SalesApiService {
  private readonly http = inject(HttpClient);
  list(filters: Record<string, string | number>) { let params = new HttpParams(); Object.entries(filters).filter(([, value]) => value !== '').forEach(([key, value]) => params = params.set(key, value)); return this.http.get<PagedSales>('/api/sales', { params }); }
  get(id: string) { return this.http.get<Sale>(`/api/sales/${id}`); }
  create(request: CreateSaleRequest, key: string) { return this.http.post<Sale>('/api/sales', request, { headers: new HttpHeaders({ 'Idempotency-Key': key }) }); }
  cancel(id: string, reason: string, rowVersion: string) { return this.http.post<Sale>(`/api/sales/${id}/cancel`, { reason, rowVersion }); }
}
