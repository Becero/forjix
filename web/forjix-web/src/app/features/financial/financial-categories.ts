import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FinancialApiService, FinancialCategory } from '../../core/api/financial-api.service';
import { apiError } from '../../core/api/api-error';
import { AuthSessionStore } from '../../core/auth/auth-session.store';
@Component({ selector: 'app-financial-categories', standalone: true, imports: [CommonModule, ReactiveFormsModule], templateUrl: './financial-categories.html' })
export class FinancialCategories {
  private readonly api = inject(FinancialApiService);
  readonly canManage = inject(AuthSessionStore).context()?.permissions.includes('financial.categories.manage') ?? false;
  readonly items = signal<FinancialCategory[]>([]); readonly modal = signal(false); readonly editing = signal<FinancialCategory | null>(null); readonly error = signal(''); readonly saving = signal(false);
  readonly filter = new FormControl('', { nonNullable: true });
  readonly form = new FormGroup({ name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }), type: new FormControl<'Income'|'Expense'>('Income', { nonNullable: true }), isActive: new FormControl(true, { nonNullable: true }) });
  constructor() { this.load(); }
  load() { this.api.categories(this.filter.value).subscribe({ next: x => this.items.set(x), error: e => this.error.set(apiError(e)) }); }
  open(item?: FinancialCategory) { this.editing.set(item ?? null); this.form.reset({ name: item?.name ?? '', type: item?.type ?? 'Income', isActive: item?.isActive ?? true }); this.error.set(''); this.modal.set(true); }
  save() { if (this.form.invalid || this.saving()) return; this.saving.set(true); this.api.saveCategory(this.form.getRawValue(), this.editing()?.id).subscribe({ next: () => { this.saving.set(false); this.modal.set(false); this.load(); }, error: e => { this.saving.set(false); this.error.set(apiError(e)); } }); }
}
