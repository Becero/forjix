import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { apiError } from '../../../core/api/api-error';
import { InventoryApiService } from '../../../core/api/inventory-api.service';
import { CreateInventoryMovementRequest, InventoryItem } from '../../../core/api/inventory.models';
import { ManagementApiService } from '../../../core/api/management-api.service';
import { CategoryItem } from '../../../core/api/management.models';
import { AuthSessionStore } from '../../../core/auth/auth-session.store';
import { InventoryHistory } from '../inventory-history/inventory-history';
import { InventoryMovementDialog } from '../inventory-movement-dialog/inventory-movement-dialog';

@Component({ selector: 'app-inventory-list', imports: [CommonModule, ReactiveFormsModule, InventoryHistory, InventoryMovementDialog], templateUrl: './inventory-list.html' })
export class InventoryList {
  private readonly api = inject(InventoryApiService); private readonly management = inject(ManagementApiService); private readonly session = inject(AuthSessionStore);
  readonly items = signal<InventoryItem[]>([]); readonly categories = signal<CategoryItem[]>([]); readonly selected = signal<InventoryItem | null>(null); readonly history = signal<InventoryItem | null>(null); readonly saving = signal(false); readonly error = signal('');
  readonly canManage = this.session.context()?.permissions.includes('stock.manage') ?? false;
  readonly allowNegativeStock = this.session.context()?.settings?.['AllowNegativeStock'] === true;
  readonly filters = new FormGroup({ search: new FormControl('', { nonNullable: true }), categoryId: new FormControl('', { nonNullable: true }), status: new FormControl('', { nonNullable: true }) });
  constructor() { this.load(); if (this.session.context()?.permissions.includes('categories.view')) this.management.categories(false).subscribe(items => this.categories.set(items)); }
  load() { this.api.list(this.filters.getRawValue()).subscribe(items => this.items.set(items)); }
  move(request: CreateInventoryMovementRequest) { const item = this.selected(); if (!item) return; this.saving.set(true); this.error.set(''); this.api.createMovement(item.productId, { ...request, rowVersion: item.rowVersion }).subscribe({ next: result => { this.saving.set(false); this.selected.set(null); this.items.update(items => items.map(current => current.productId === result.inventory.productId ? result.inventory : current)); }, error: err => { this.saving.set(false); this.error.set(apiError(err)); } }); }
  statusLabel(status: string) { return ({ Normal: 'Normal', Low: 'Estoque baixo', OutOfStock: 'Sem estoque', Negative: 'Negativo' } as Record<string, string>)[status] ?? status; }
}
