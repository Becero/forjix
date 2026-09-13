import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { apiError } from '../../core/api/api-error';
import { ManagementApiService } from '../../core/api/management-api.service';
import { ProductItem } from '../../core/api/management.models';
import { PurchaseList, PurchasesApiService, Supplier } from '../../core/api/purchases-api.service';
import { AuthSessionStore } from '../../core/auth/auth-session.store';

interface Draft { product: ProductItem; quantity: number; unitCost: number; }
@Component({ selector: 'app-purchases', imports: [CommonModule, ReactiveFormsModule], templateUrl: './purchases.html' })
export class Purchases {
  private api = inject(PurchasesApiService); private management = inject(ManagementApiService); private context = inject(AuthSessionStore).context;
  items = signal<PurchaseList[]>([]); total = signal(0); suppliers = signal<Supplier[]>([]); products = signal<ProductItem[]>([]); draft = signal<Draft[]>([]); modal = signal(false); confirmation = signal<{ item: PurchaseList; action: 'receive' | 'cancel' } | null>(null); error = signal('');
  canManage = this.context()?.permissions.includes('purchases.manage') ?? false; canReceive = this.context()?.permissions.includes('purchases.receive') ?? false;
  filters = new FormGroup({ search: new FormControl('', { nonNullable: true }), status: new FormControl('', { nonNullable: true }), page: new FormControl(1, { nonNullable: true }), pageSize: new FormControl(20, { nonNullable: true }) });
  form = new FormGroup({ supplierId: new FormControl('', { nonNullable: true, validators: Validators.required }), notes: new FormControl('', { nonNullable: true }), productId: new FormControl('', { nonNullable: true }), quantity: new FormControl(1, { nonNullable: true }), unitCost: new FormControl(0, { nonNullable: true }) });
  constructor() { this.load(); this.api.suppliers({ isActive: 'true', page: 1, pageSize: 100 }).subscribe(result => this.suppliers.set(result.items)); this.management.products({ isActive: 'true', page: 1, pageSize: 100 }).subscribe(result => this.products.set(result.items)); }
  load(resetPage = false) { if (resetPage) this.filters.controls.page.setValue(1); this.api.purchases(this.filters.getRawValue()).subscribe({ next: result => { this.items.set(result.items); this.total.set(result.total); }, error: e => this.error.set(apiError(e)) }); }
  page(delta: number) { this.filters.controls.page.setValue(this.filters.controls.page.value + delta); this.load(); }
  add() { const product = this.products().find(x => x.id === this.form.controls.productId.value); const quantity = Number(this.form.controls.quantity.value), unitCost = Number(this.form.controls.unitCost.value); if (!product || quantity <= 0 || unitCost < 0) return; this.draft.update(items => [...items.filter(x => x.product.id !== product.id), { product, quantity, unitCost }]); }
  remove(id: string) { this.draft.update(items => items.filter(x => x.product.id !== id)); }
  save() { if (!this.form.controls.supplierId.value || !this.draft().length) return; this.api.create({ supplierId: this.form.controls.supplierId.value, notes: this.form.controls.notes.value, items: this.draft().map(x => ({ productId: x.product.id, quantity: x.quantity, unitCost: x.unitCost })) }).subscribe({ next: () => { this.modal.set(false); this.draft.set([]); this.load(); }, error: e => this.error.set(apiError(e)) }); }
  confirmAction(item: PurchaseList, action: 'receive' | 'cancel') { this.confirmation.set({ item, action }); }
  executeAction() { const choice = this.confirmation(); if (!choice) return; this.api.action(choice.item.id, choice.action, choice.item.rowVersion).subscribe({ next: () => { this.confirmation.set(null); this.load(); }, error: e => this.error.set(apiError(e)) }); }
}
