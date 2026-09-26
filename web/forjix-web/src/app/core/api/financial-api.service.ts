import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

export type FinancialKind = 'accounts-receivable' | 'accounts-payable';
export interface FinancialCategory { id: string; name: string; type: 'Income' | 'Expense'; isActive: boolean; }
export interface FinancialAccount {
  id: string; groupId: string; partyId?: string; partyName?: string; sourceId?: string; financialCategoryId: string; categoryName: string;
  description: string; document?: string; originalAmount: number; openAmount: number; issueDate: string; dueDate: string; paymentDate?: string;
  status: string; installmentNumber: number; totalInstallments: number; notes?: string; rowVersion: string;
}
export interface FinancialPayment { id: string; amount: number; principalAmount: number; paymentDate: string; paymentMethod: string; discount: number; interest: number; penalty: number; notes?: string; cashMovementId?: number; reversedAt?: string; reversalReason?: string; rowVersion: string; }
export interface FinancialDashboard { totalReceivable: number; totalPayable: number; overdueCount: number; overdueAmount: number; received: number; paid: number; balance: number; upcoming: FinancialAccount[]; }
export interface SaveFinancialAccount { partyId: string | null; financialCategoryId: string; description: string; document: string; amount: number; issueDate: string; dueDate: string; installments: number; notes: string; rowVersion: string; }
export interface PaymentRequest { amount: number; paymentDate: string; paymentMethod: string; discount: number; interest: number; penalty: number; notes: string; rowVersion: string; }
export interface FinancialTerms { financialCategoryId: string; firstDueDate: string; installments: number; }
export const financialStatusLabels: Record<string, string> = { Pending: 'Pendente', PartiallyPaid: 'Parcialmente paga', Paid: 'Paga', Overdue: 'Vencida', Cancelled: 'Cancelada' };

@Injectable({ providedIn: 'root' })
export class FinancialApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/financial';
  private params(filters: Record<string, string | number | boolean>) {
    let params = new HttpParams();
    Object.entries(filters).filter(([, value]) => value !== '').forEach(([key, value]) => params = params.set(key, value));
    return params;
  }
  categories(type = '') { return this.http.get<FinancialCategory[]>(this.base + '/categories', { params: this.params({ type }) }); }
  saveCategory(value: Omit<FinancialCategory, 'id'>, id?: string) {
    return id ? this.http.put<FinancialCategory>(this.base + '/categories/' + id, value) : this.http.post<FinancialCategory>(this.base + '/categories', value);
  }
  list(kind: FinancialKind, filters: Record<string, string | number | boolean>) { return this.http.get<{ items: FinancialAccount[]; total: number }>(this.base + '/' + kind, { params: this.params(filters) }); }
  get(kind: FinancialKind, id: string) { return this.http.get<FinancialAccount>(this.base + '/' + kind + '/' + id); }
  create(kind: FinancialKind, value: SaveFinancialAccount) { return this.http.post<FinancialAccount[]>(this.base + '/' + kind, value); }
  update(kind: FinancialKind, id: string, value: SaveFinancialAccount) { return this.http.put<FinancialAccount>(this.base + '/' + kind + '/' + id, value); }
  payments(kind: FinancialKind, id: string) { return this.http.get<FinancialPayment[]>(this.base + '/' + kind + '/' + id + '/payments'); }
  pay(kind: FinancialKind, id: string, value: PaymentRequest, key: string) { return this.http.post<FinancialAccount>(this.base + '/' + kind + '/' + id + '/payments', value, { headers: new HttpHeaders({ 'Idempotency-Key': key }) }); }
  cancel(kind: FinancialKind, id: string, reason: string, rowVersion: string) { return this.http.post<FinancialAccount>(this.base + '/' + kind + '/' + id + '/cancel', { reason, rowVersion }); }
  reverse(kind: FinancialKind, id: string, paymentId: string, reason: string, rowVersion: string) { return this.http.post<FinancialAccount>(this.base + '/' + kind + '/' + id + '/payments/' + paymentId + '/reverse', { reason, rowVersion }); }
  dashboard(from: string, through: string) { return this.http.get<FinancialDashboard>(this.base + '/dashboard', { params: this.params({ from, through }) }); }
}
