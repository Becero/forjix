import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { apiError } from '../../core/api/api-error';
import { SalesApiService } from '../../core/api/sales-api.service';
import { SaleListItem } from '../../core/api/sales.models';
import { AuthSessionStore } from '../../core/auth/auth-session.store';

@Component({ selector: 'app-sales', imports: [CommonModule, ReactiveFormsModule], templateUrl: './sales.html' })
export class Sales {
  private readonly api = inject(SalesApiService); private readonly context = inject(AuthSessionStore).context;
  readonly items = signal<SaleListItem[]>([]); readonly total = signal(0); readonly selected = signal<SaleListItem | null>(null); readonly error = signal(''); readonly loading = signal(false);
  readonly canCancel = this.context()?.permissions.includes('sales.cancel') ?? false;
  readonly filters = new FormGroup({ from: new FormControl('', { nonNullable: true }), to: new FormControl('', { nonNullable: true }), status: new FormControl('', { nonNullable: true }), page: new FormControl(1, { nonNullable: true }), pageSize: new FormControl(20, { nonNullable: true }) });
  readonly cancelForm = new FormGroup({ reason: new FormControl('', { nonNullable: true }) });
  constructor() { this.load(); }
  load() { this.loading.set(true); this.api.list(this.filters.getRawValue()).subscribe({ next: result => { this.items.set(result.items); this.total.set(result.total); this.loading.set(false); }, error: error => { this.error.set(apiError(error)); this.loading.set(false); } }); }
  page(delta: number) { this.filters.controls.page.setValue(this.filters.controls.page.value + delta); this.load(); }
  cancel() { const sale = this.selected(); const reason = this.cancelForm.controls.reason.value.trim(); if (!sale || !reason) { this.error.set('Informe o motivo do cancelamento.'); return; } this.api.cancel(sale.id, reason, sale.rowVersion).subscribe({ next: () => { this.selected.set(null); this.cancelForm.reset(); this.load(); }, error: error => this.error.set(apiError(error)) }); }
  payment(value: string) { return ({ Cash: 'Dinheiro', Pix: 'PIX', CreditCard: 'Crédito', DebitCard: 'Débito' } as Record<string,string>)[value] ?? value; }
}
