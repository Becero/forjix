import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
export interface Customer { id: string; name: string; document?: string; email?: string; phone?: string; notes?: string; isActive: boolean; createdAt: string; updatedAt: string; }
export interface SaveCustomer { name: string; document?: string | null; email?: string | null; phone?: string | null; notes?: string | null; isActive: boolean; }
export interface PagedCustomers { items: Customer[]; page: number; pageSize: number; total: number; }
@Injectable({ providedIn: 'root' }) export class CustomersApiService { private readonly http = inject(HttpClient); list(filters: Record<string,string|number>) { let params = new HttpParams(); Object.entries(filters).filter(([,v]) => v !== '').forEach(([k,v]) => params=params.set(k,v)); return this.http.get<PagedCustomers>('/api/customers',{params}); } create(body: SaveCustomer) { return this.http.post<Customer>('/api/customers',body); } update(id:string,body:SaveCustomer) { return this.http.put<Customer>(`/api/customers/${id}`,body); } }
