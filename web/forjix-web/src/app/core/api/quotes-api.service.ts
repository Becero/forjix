import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { FinancialTerms } from './financial-api.service';
export interface QuoteItem { id: string; productId: string; productName: string; sku: string; quantity: number; unitPrice: number; discount: number; total: number; }
export interface Quote { id: string; number: string; customerId: string; customerName: string; issueDate: string; validUntil: string; status: string; notes?: string; subtotal: number; discount: number; total: number; userId: string; userName: string; saleId?: string; convertedAt?: string; rowVersion: string; items: QuoteItem[]; isEditable?: boolean; }
export interface SaveQuote { customerId: string; validUntil: string; discount: number; notes: string; rowVersion?: string; items: { productId: string; quantity: number; discount: number }[]; }
export interface QuoteSummary { count: number; value: number; approved: number; pending: number; converted: number; expired: number; conversionRate: number; }
export const quoteStatusLabels: Record<string, string> = { Draft: 'Rascunho', Sent: 'Enviado', Approved: 'Aprovado', Rejected: 'Rejeitado', Expired: 'Vencido', Converted: 'Convertido', Cancelled: 'Cancelado' };
@Injectable({ providedIn: 'root' })
export class QuotesApiService {
  private readonly http = inject(HttpClient); private readonly base = '/api/quotes';
  list(filters: Record<string, string | number | boolean>) {
    let params = new HttpParams();
    Object.entries(filters).filter(([,v]) => v !== '').forEach(([key,v]) => params = params.set(key,v));
    return this.http.get<{ items: Quote[]; total: number }>(this.base, { params });
  }
  get(id: string) { return this.http.get<Quote>(this.base + '/' + id); }
  save(value: SaveQuote, id?: string) { return id ? this.http.put<Quote>(this.base + '/' + id, value) : this.http.post<Quote>(this.base, value); }
  action(id: string, action: 'send'|'approve'|'reject'|'cancel', rowVersion: string, reason?: string) { return this.http.post<Quote>(this.base + '/' + id + '/' + action, { rowVersion, reason }); }
  convert(id: string, rowVersion: string, paymentMethod: string, financialTerms?: FinancialTerms) { return this.http.post<{ id: string; number: string }>(this.base + '/' + id + '/convert-to-sale', { rowVersion, paymentMethod, financialTerms }); }
  pdf(id: string) { return this.http.get(this.base + '/' + id + '/pdf', { responseType: 'blob' }); }
  summary() { return this.http.get<QuoteSummary>(this.base + '/summary'); }
}
