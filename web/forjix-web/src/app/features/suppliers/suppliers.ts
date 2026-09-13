import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { apiError } from '../../core/api/api-error';
import { PurchasesApiService, Supplier } from '../../core/api/purchases-api.service';
import { AuthSessionStore } from '../../core/auth/auth-session.store';

@Component({ selector: 'app-suppliers', imports: [CommonModule, ReactiveFormsModule], templateUrl: './suppliers.html' })
export class Suppliers {
  private api = inject(PurchasesApiService); private context = inject(AuthSessionStore).context;
  items = signal<Supplier[]>([]); total = signal(0); editing = signal<Supplier | null>(null); modal = signal(false); error = signal('');
  canManage = this.context()?.permissions.includes('suppliers.manage') ?? false;
  filters = new FormGroup({ search: new FormControl('', { nonNullable: true }), isActive: new FormControl('', { nonNullable: true }), page: new FormControl(1, { nonNullable: true }), pageSize: new FormControl(20, { nonNullable: true }) });
  form = new FormGroup({ name: new FormControl('', { nonNullable: true, validators: Validators.required }), document: new FormControl('', { nonNullable: true }), email: new FormControl('', { nonNullable: true, validators: Validators.email }), phone: new FormControl('', { nonNullable: true }), contactName: new FormControl('', { nonNullable: true }), notes: new FormControl('', { nonNullable: true }), isActive: new FormControl(true, { nonNullable: true }) });
  constructor() { this.load(); }
  load(resetPage = false) { if (resetPage) this.filters.controls.page.setValue(1); this.api.suppliers(this.filters.getRawValue()).subscribe({ next: result => { this.items.set(result.items); this.total.set(result.total); }, error: e => this.error.set(apiError(e)) }); }
  page(delta: number) { this.filters.controls.page.setValue(this.filters.controls.page.value + delta); this.load(); }
  open(item?: Supplier) { this.editing.set(item ?? null); this.form.reset({ name: item?.name ?? '', document: item?.document ?? '', email: item?.email ?? '', phone: item?.phone ?? '', contactName: item?.contactName ?? '', notes: item?.notes ?? '', isActive: item?.isActive ?? true }); this.modal.set(true); }
  save() { if (this.form.invalid) return; this.api.saveSupplier(this.form.getRawValue(), this.editing()?.id).subscribe({ next: () => { this.modal.set(false); this.load(); }, error: e => this.error.set(apiError(e)) }); }
}
