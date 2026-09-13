import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { InventoryApiService } from '../../../core/api/inventory-api.service';
import { InventoryItem, InventoryMovementItem } from '../../../core/api/inventory.models';

@Component({ selector: 'app-inventory-history', imports: [CommonModule, ReactiveFormsModule], templateUrl: './inventory-history.html' })
export class InventoryHistory implements OnChanges {
  @Input({ required: true }) item!: InventoryItem;
  private readonly api = inject(InventoryApiService);
  readonly movements = signal<InventoryMovementItem[]>([]); readonly total = signal(0); readonly page = signal(1); readonly pageSize = 20;
  readonly filters = new FormGroup({ from: new FormControl('', { nonNullable: true }), to: new FormControl('', { nonNullable: true }), type: new FormControl('', { nonNullable: true }), user: new FormControl('', { nonNullable: true }) });
  ngOnChanges() { this.load(1); }
  load(page = this.page()) { this.page.set(page); this.api.movements(this.item.productId, { ...this.filters.getRawValue(), page, pageSize: this.pageSize }).subscribe(result => { this.movements.set(result.items); this.total.set(result.total); }); }
  label(type: string) { return ({ StockEntry: 'Entrada', StockExit: 'Saída', PositiveAdjustment: 'Ajuste positivo', NegativeAdjustment: 'Ajuste negativo' } as Record<string, string>)[type] ?? type; }
}
