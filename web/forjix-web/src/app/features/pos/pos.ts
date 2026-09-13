import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { apiError } from '../../core/api/api-error';
import { CashApiService } from '../../core/api/cash-api.service';
import { Customer, CustomersApiService } from '../../core/api/customers-api.service';
import { InventoryApiService } from '../../core/api/inventory-api.service';
import { InventoryItem } from '../../core/api/inventory.models';
import { SalesApiService } from '../../core/api/sales-api.service';
import { PaymentMethod, Sale } from '../../core/api/sales.models';
import { AuthSessionStore } from '../../core/auth/auth-session.store';

interface CartLine { product: InventoryItem; quantity: number; }

@Component({ selector: 'app-pos', imports: [CommonModule, ReactiveFormsModule, RouterLink], templateUrl: './pos.html' })
export class Pos {
  private readonly inventory = inject(InventoryApiService);
  private readonly sales = inject(SalesApiService);
  private readonly customersApi = inject(CustomersApiService);
  private readonly cashApi = inject(CashApiService);
  private readonly context = inject(AuthSessionStore).context;
  readonly products = signal<InventoryItem[]>([]);
  readonly customers = signal<Customer[]>([]);
  readonly cart = signal<CartLine[]>([]);
  readonly error = signal('');
  readonly success = signal<Sale | null>(null);
  readonly saving = signal(false);
  readonly cashOpen = signal<boolean | null>(null);
  readonly canDiscount = this.context()?.permissions.includes('sales.discount') ?? false;
  readonly canViewCustomers = this.context()?.permissions.includes('customers.view') ?? false;
  readonly canViewCash = this.context()?.permissions.includes('cash.view') ?? false;
  readonly allowNegative = this.context()?.settings?.['AllowNegativeStock'] === true;
  readonly form = new FormGroup({ search: new FormControl('', { nonNullable: true }), customerId: new FormControl('', { nonNullable: true }), discount: new FormControl(0, { nonNullable: true }), paymentMethod: new FormControl<PaymentMethod>('Cash', { nonNullable: true }) });
  readonly subtotal = computed(() => this.cart().reduce((sum, line) => sum + line.product.salePrice * line.quantity, 0));
  readonly total = computed(() => Math.max(0, this.subtotal() - Number(this.form.controls.discount.value || 0)));

  constructor() {
    this.load();
    if (this.canViewCustomers) this.customersApi.list({ isActive: 'true', page: 1, pageSize: 100 }).subscribe(result => this.customers.set(result.items));
    if (this.canViewCash) this.refreshCash();
    this.form.controls.discount.valueChanges.subscribe(() => this.cart.update(lines => [...lines]));
  }

  load() { this.inventory.list({ search: this.form.controls.search.value }).subscribe({ next: products => this.products.set(products), error: error => this.error.set(apiError(error)) }); }
  refreshCash() { this.cashApi.current().subscribe({ next: session => this.cashOpen.set(session?.status === 'Open'), error: () => this.cashOpen.set(false) }); }
  available(product: InventoryItem) { return this.allowNegative || product.quantity > 0; }
  add(product: InventoryItem) { if (!this.available(product)) return; const found = this.cart().find(x => x.product.productId === product.productId); if (found) this.change(product.productId, 1); else this.cart.update(lines => [...lines, { product, quantity: 1 }]); }
  change(id: string, delta: number) { this.cart.update(lines => lines.map(x => x.product.productId === id ? { ...x, quantity: x.quantity + delta } : x).filter(x => x.quantity > 0)); }
  remove(id: string) { this.cart.update(lines => lines.filter(x => x.product.productId !== id)); }
  finish() {
    const discount = Number(this.form.controls.discount.value || 0);
    if (!this.cart().length || discount < 0 || discount > this.subtotal()) { this.error.set('Revise os itens e o desconto.'); return; }
    if (this.cashOpen() === false) { this.error.set('Abra o caixa antes de finalizar uma venda.'); return; }
    this.saving.set(true); this.error.set('');
    const customerId = this.form.controls.customerId.value || null;
    const request = { paymentMethod: this.form.controls.paymentMethod.value, discount, customerId, items: this.cart().map(x => ({ productId: x.product.productId, quantity: x.quantity })) };
    this.sales.create(request, crypto.randomUUID()).subscribe({ next: sale => { this.success.set(sale); this.cart.set([]); this.form.controls.discount.setValue(0); this.saving.set(false); this.load(); if (this.canViewCash) this.refreshCash(); }, error: error => { this.error.set(apiError(error)); this.saving.set(false); } });
  }
}
