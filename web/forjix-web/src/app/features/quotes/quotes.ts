import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { apiError } from '../../core/api/api-error';
import { CustomersApiService } from '../../core/api/customers-api.service';
import { ManagementApiService } from '../../core/api/management-api.service';
import { ProductItem } from '../../core/api/management.models';
import { FinancialApiService, FinancialCategory } from '../../core/api/financial-api.service';
import { Quote, QuotesApiService, quoteStatusLabels } from '../../core/api/quotes-api.service';
import { AuthSessionStore } from '../../core/auth/auth-session.store';
const date = (days = 0) => { const value = new Date(); value.setUTCDate(value.getUTCDate() + days); return value.toISOString().slice(0,10); };
type ItemForm = FormGroup<{ productId: FormControl<string>; productName: FormControl<string>; unitPrice: FormControl<number>; quantity: FormControl<number>; discount: FormControl<number> }>;
type Action = 'send'|'approve'|'reject'|'cancel'|'convert';
@Component({ selector: 'app-quotes', standalone: true, imports: [CommonModule, ReactiveFormsModule], templateUrl: './quotes.html' })
export class Quotes {
  private readonly api = inject(QuotesApiService); private readonly context = inject(AuthSessionStore).context;
  readonly customers = signal<{ id: string; name: string }[]>([]); readonly products = signal<ProductItem[]>([]);
  readonly financialCategories = signal<FinancialCategory[]>([]);
  readonly items = signal<Quote[]>([]); readonly total = signal(0); readonly loading = signal(false); readonly saving = signal(false);
  readonly error = signal(''); readonly success = signal(''); readonly modal = signal(false); readonly editing = signal<Quote|null>(null); readonly detail = signal<Quote|null>(null);
  readonly choice = signal<{ quote: Quote; action: Action }|null>(null); readonly labels = quoteStatusLabels;
  readonly product = new FormControl('', { nonNullable: true });
  readonly filters = new FormGroup({ search: new FormControl('', { nonNullable: true }), customerId: new FormControl('', { nonNullable: true }), status: new FormControl('', { nonNullable: true }), from: new FormControl('', { nonNullable: true }), through: new FormControl('', { nonNullable: true }), expiredOnly: new FormControl(false, { nonNullable: true }), convertedOnly: new FormControl(false, { nonNullable: true }), page: new FormControl(1, { nonNullable: true }), pageSize: new FormControl(20, { nonNullable: true }) });
  readonly form = new FormGroup({ customerId: new FormControl('', { nonNullable: true, validators: Validators.required }), validUntil: new FormControl(date(15), { nonNullable: true, validators: Validators.required }), discount: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }), notes: new FormControl('', { nonNullable: true, validators: Validators.maxLength(2000) }), items: new FormArray<ItemForm>([]) });
  readonly actionForm = new FormGroup({ reason: new FormControl('', { nonNullable: true, validators: Validators.maxLength(500) }), paymentMethod: new FormControl('Pix', { nonNullable: true }), financialCategoryId: new FormControl('', { nonNullable: true }), firstDueDate: new FormControl(date(30), { nonNullable: true }), installments: new FormControl(1, { nonNullable: true, validators: [Validators.min(1), Validators.max(120)] }) });
  constructor() {
    this.load();
    if (this.can('customers.view')) inject(CustomersApiService).list({ page: 1, pageSize: 100, isActive: 'true' }).subscribe({ next: x => this.customers.set(x.items), error: e => this.error.set(apiError(e)) });
    if (this.can('products.view')) inject(ManagementApiService).products({ page: 1, pageSize: 100, isActive: 'true' }).subscribe({ next: x => this.products.set(x.items), error: e => this.error.set(apiError(e)) });
    if (this.can('financial.categories.view')) inject(FinancialApiService).categories('Income').subscribe({ next: x => this.financialCategories.set(x.filter(c => c.isActive)), error: e => this.error.set(apiError(e)) });
  }
  can(permission: string) { return this.context()?.permissions.includes(permission) ?? false; }
  canAction(quote: Quote, action: Action) {
    if (action === 'convert') return quote.status === 'Approved' && this.can('quotes.convert') && this.can('sales.create');
    if (action === 'cancel') return ['Draft','Sent','Approved','Expired'].includes(quote.status) && this.can('quotes.cancel');
    return this.can('quotes.status') && (action === 'send' ? quote.status === 'Draft' : quote.status === 'Sent');
  }
  load(reset = false) {
    if (reset) this.filters.controls.page.setValue(1); this.loading.set(true);
    this.api.list(this.filters.getRawValue()).subscribe({ next: x => { this.items.set(x.items); this.total.set(x.total); this.loading.set(false); }, error: e => { this.loading.set(false); this.error.set(apiError(e)); } });
  }
  page(delta: number) { this.filters.controls.page.setValue(this.filters.controls.page.value + delta); this.load(); }
  open(quote?: Quote) {
    this.editing.set(quote ?? null); this.error.set(''); this.form.controls.items.clear();
    this.form.patchValue({ customerId: quote?.customerId ?? '', validUntil: quote?.validUntil ?? date(15), discount: quote?.discount ?? 0, notes: quote?.notes ?? '' });
    quote?.items.forEach(item => this.addRow(item.productId, item.productName, item.unitPrice, item.quantity, item.discount));
    this.modal.set(true);
  }
  private addRow(productId: string, productName: string, unitPrice: number, quantity: number, discount: number) {
    this.form.controls.items.push(new FormGroup({ productId: new FormControl(productId, { nonNullable: true }), productName: new FormControl(productName, { nonNullable: true }), unitPrice: new FormControl(unitPrice, { nonNullable: true }), quantity: new FormControl(quantity, { nonNullable: true, validators: [Validators.required, Validators.min(0.001)] }), discount: new FormControl(discount, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }) }));
  }
  add() {
    const product = this.products().find(x => x.id === this.product.value); if (!product) return;
    if (this.form.controls.items.controls.some(x => x.controls.productId.value === product.id)) { this.error.set('Produto já incluído; ajuste sua quantidade.'); return; }
    this.addRow(product.id, product.name, product.salePrice, 1, 0); this.product.setValue('');
  }
  preview() { return this.form.controls.items.getRawValue().reduce((sum, x) => sum + Math.round(x.quantity * x.unitPrice * 100) / 100 - x.discount, 0) - this.form.controls.discount.value; }
  save() {
    if (this.saving()) return;
    if (this.form.invalid || !this.form.controls.items.length || this.preview() < 0) { this.form.markAllAsTouched(); this.error.set('Revise cliente, validade, itens e descontos.'); return; }
    const raw = this.form.getRawValue(); const quote = this.editing(); this.saving.set(true);
    this.api.save({ customerId: raw.customerId, validUntil: raw.validUntil, discount: raw.discount, notes: raw.notes, rowVersion: quote?.rowVersion, items: raw.items.map(x => ({ productId: x.productId, quantity: x.quantity, discount: x.discount })) }, quote?.id).subscribe({ next: x => { this.saving.set(false); this.modal.set(false); this.success.set('Orçamento salvo: ' + x.number); this.load(); }, error: e => { this.saving.set(false); this.error.set(apiError(e)); } });
  }
  view(quote: Quote) { this.error.set(''); this.api.get(quote.id).subscribe({ next: x => this.detail.set(x), error: e => this.error.set(apiError(e)) }); }
  confirm(quote: Quote, action: Action) {
    this.error.set(''); this.actionForm.reset({ reason: '', paymentMethod: 'Pix', financialCategoryId: '', firstDueDate: date(30), installments: 1 }); this.choice.set({ quote, action });
  }
  execute() {
    const choice = this.choice(); if (!choice || this.saving() || this.actionForm.invalid) return;
    const values = this.actionForm.getRawValue();
    if (['reject','cancel'].includes(choice.action) && !values.reason.trim()) { this.error.set('Informe o motivo.'); return; }
    if (choice.action === 'convert' && values.paymentMethod === 'Deferred' && (!values.financialCategoryId || !values.firstDueDate)) { this.error.set('Informe as condições da venda a prazo.'); return; }
    this.saving.set(true);
    if (choice.action === 'convert') {
      this.api.convert(choice.quote.id, choice.quote.rowVersion, values.paymentMethod, values.paymentMethod === 'Deferred' ? { financialCategoryId: values.financialCategoryId, firstDueDate: values.firstDueDate, installments: values.installments } : undefined).subscribe({ next: x => { this.saving.set(false); this.choice.set(null); this.detail.set(null); this.success.set('Venda criada: ' + x.number); this.load(); }, error: e => { this.saving.set(false); this.error.set(apiError(e)); } });
    } else {
      this.api.action(choice.quote.id, choice.action, choice.quote.rowVersion, values.reason).subscribe({ next: x => { this.saving.set(false); this.choice.set(null); if (this.detail()) this.detail.set(x); this.success.set('Status atualizado.'); this.load(); }, error: e => { this.saving.set(false); this.error.set(apiError(e)); } });
    }
  }
  pdf(quote: Quote) {
    this.api.pdf(quote.id).subscribe({ next: blob => { const url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = quote.number + '.pdf'; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); }, error: async e => { if (e.error instanceof Blob) { try { this.error.set(JSON.parse(await e.error.text()).detail); return; } catch {} } this.error.set(apiError(e)); } });
  }
}
