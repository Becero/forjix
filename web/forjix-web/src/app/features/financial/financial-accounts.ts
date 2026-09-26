import { Observable } from 'rxjs';
import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { apiError } from '../../core/api/api-error';
import { CustomersApiService } from '../../core/api/customers-api.service';
import { PurchasesApiService } from '../../core/api/purchases-api.service';
import { FinancialAccount, FinancialApiService, FinancialCategory, FinancialKind, FinancialPayment, financialStatusLabels } from '../../core/api/financial-api.service';
import { AuthSessionStore } from '../../core/auth/auth-session.store';

const today = () => new Date().toISOString().slice(0, 10);
@Component({ selector: 'app-financial-accounts', standalone: true, imports: [CommonModule, ReactiveFormsModule], templateUrl: './financial-accounts.html' })
export class FinancialAccounts {
  private readonly api = inject(FinancialApiService);
  private readonly context = inject(AuthSessionStore).context;
  readonly kind = inject(ActivatedRoute).snapshot.data['kind'] as FinancialKind;
  readonly receivable = this.kind === 'accounts-receivable';
  readonly title = this.receivable ? 'Contas a receber' : 'Contas a pagar';
  readonly partyLabel = this.receivable ? 'Cliente' : 'Fornecedor';
  private readonly permission = this.receivable ? 'financial.receivable' : 'financial.payable';
  readonly canManage = this.can(this.permission + '.manage');
  readonly canPay = this.can(this.permission + '.pay');
  readonly items = signal<FinancialAccount[]>([]); readonly total = signal(0); readonly loading = signal(false); readonly saving = signal(false);
  readonly categories = signal<FinancialCategory[]>([]); readonly parties = signal<{ id: string; name: string }[]>([]);
  readonly error = signal(''); readonly success = signal(''); readonly editor = signal(false); readonly editing = signal<FinancialAccount | null>(null);
  readonly detail = signal<FinancialAccount | null>(null); readonly payments = signal<FinancialPayment[]>([]); readonly paying = signal<FinancialAccount | null>(null);
  readonly action = signal<{ account: FinancialAccount; payment?: FinancialPayment } | null>(null);
  readonly statusLabels = financialStatusLabels;
  readonly filters = new FormGroup({ search: new FormControl('', { nonNullable: true }), from: new FormControl('', { nonNullable: true }), through: new FormControl('', { nonNullable: true }), status: new FormControl('', { nonNullable: true }), partyId: new FormControl('', { nonNullable: true }), categoryId: new FormControl('', { nonNullable: true }), overdueOnly: new FormControl(false, { nonNullable: true }), page: new FormControl(1, { nonNullable: true }), pageSize: new FormControl(20, { nonNullable: true }) });
  readonly form = new FormGroup({ partyId: new FormControl('', { nonNullable: true }), financialCategoryId: new FormControl('', { nonNullable: true, validators: Validators.required }), description: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }), document: new FormControl('', { nonNullable: true }), amount: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0.01)] }), issueDate: new FormControl(today(), { nonNullable: true, validators: Validators.required }), dueDate: new FormControl(today(), { nonNullable: true, validators: Validators.required }), installments: new FormControl(1, { nonNullable: true, validators: [Validators.min(1), Validators.max(120)] }), notes: new FormControl('', { nonNullable: true }), rowVersion: new FormControl('', { nonNullable: true }) });
  readonly paymentForm = new FormGroup({ amount: new FormControl(0, { nonNullable: true, validators: Validators.min(0.01) }), paymentDate: new FormControl(today(), { nonNullable: true, validators: Validators.required }), paymentMethod: new FormControl('Cash', { nonNullable: true }), discount: new FormControl(0, { nonNullable: true, validators: Validators.min(0) }), interest: new FormControl(0, { nonNullable: true, validators: Validators.min(0) }), penalty: new FormControl(0, { nonNullable: true, validators: Validators.min(0) }), notes: new FormControl('', { nonNullable: true }), rowVersion: new FormControl('', { nonNullable: true }) });
  readonly actionForm = new FormGroup({ reason: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(500)] }) });
  private paymentKey = '';

  constructor() {
    this.load();
    if (this.can('financial.categories.view')) this.api.categories(this.receivable ? 'Income' : 'Expense').subscribe({ next: x => this.categories.set(x), error: e => this.error.set(apiError(e)) });
    if (this.receivable && this.can('customers.view')) inject(CustomersApiService).list({ page: 1, pageSize: 100, isActive: 'true' }).subscribe(x => this.parties.set(x.items));
    if (!this.receivable && this.can('suppliers.view')) inject(PurchasesApiService).suppliers({ page: 1, pageSize: 100, isActive: 'true' }).subscribe(x => this.parties.set(x.items));
  }
  can(code: string) { return this.context()?.permissions.includes(code) ?? false; }
  load(reset = false) {
    if (reset) this.filters.controls.page.setValue(1);
    this.loading.set(true);
    this.api.list(this.kind, this.filters.getRawValue()).subscribe({ next: x => { this.items.set(x.items); this.total.set(x.total); this.loading.set(false); }, error: e => { this.error.set(apiError(e)); this.loading.set(false); } });
  }
  page(delta: number) { this.filters.controls.page.setValue(this.filters.controls.page.value + delta); this.load(); }
  open(account?: FinancialAccount) {
    this.editing.set(account ?? null); this.error.set('');
    this.form.reset({ partyId: account?.partyId ?? '', financialCategoryId: account?.financialCategoryId ?? '', description: account?.description ?? '', document: account?.document ?? '', amount: account?.originalAmount ?? 0, issueDate: account?.issueDate ?? today(), dueDate: account?.dueDate ?? today(), installments: account?.totalInstallments ?? 1, notes: account?.notes ?? '', rowVersion: account?.rowVersion ?? '' });
    this.editor.set(true);
  }
  save() {
    if (this.saving()) return;
    if (this.form.invalid) { this.form.markAllAsTouched(); this.error.set('Revise os campos obrigatórios.'); return; }
    this.saving.set(true); const raw = this.form.getRawValue(); const value = { ...raw, partyId: raw.partyId || null }; const account = this.editing();
    const call: Observable<FinancialAccount | FinancialAccount[]> = account ? this.api.update(this.kind, account.id, value) : this.api.create(this.kind, value);
    call.subscribe({ next: () => { this.editor.set(false); this.saving.set(false); this.success.set('Conta salva.'); this.load(); }, error: e => { this.saving.set(false); this.error.set(apiError(e)); } });
  }
  view(account: FinancialAccount) {
    this.error.set(''); this.payments.set([]); this.detail.set(null);
    this.api.get(this.kind, account.id).subscribe({ next: x => this.detail.set(x), error: e => this.error.set(apiError(e)) });
    this.api.payments(this.kind, account.id).subscribe({ next: x => this.payments.set(x), error: e => this.error.set(apiError(e)) });
  }
  pay(account: FinancialAccount) {
    this.paying.set(account); this.error.set(''); this.paymentKey = crypto.randomUUID();
    this.paymentForm.reset({ amount: account.openAmount, paymentDate: today(), paymentMethod: 'Cash', discount: 0, interest: 0, penalty: 0, notes: '', rowVersion: account.rowVersion });
  }
  submitPayment() {
    const account = this.paying(); if (!account || this.paymentForm.invalid || this.saving()) return;
    this.saving.set(true);
    this.api.pay(this.kind, account.id, this.paymentForm.getRawValue(), this.paymentKey).subscribe({ next: () => { this.saving.set(false); this.paying.set(null); this.success.set('Baixa registrada.'); this.load(); }, error: e => { this.saving.set(false); this.error.set(apiError(e)); } });
  }
  confirm(account: FinancialAccount, payment?: FinancialPayment) { this.action.set({ account, payment }); this.actionForm.reset({ reason: '' }); this.error.set(''); }
  execute() {
    const choice = this.action(); if (!choice || this.actionForm.invalid || this.saving()) return;
    const reason = this.actionForm.controls.reason.value; this.saving.set(true);
    const call = choice.payment ? this.api.reverse(this.kind, choice.account.id, choice.payment.id, reason, choice.payment.rowVersion) : this.api.cancel(this.kind, choice.account.id, reason, choice.account.rowVersion);
    call.subscribe({ next: x => { this.saving.set(false); this.action.set(null); this.success.set('Operação concluída.'); this.load(); if (this.detail()) this.view(x); }, error: e => { this.saving.set(false); this.error.set(apiError(e)); } });
  }
}
