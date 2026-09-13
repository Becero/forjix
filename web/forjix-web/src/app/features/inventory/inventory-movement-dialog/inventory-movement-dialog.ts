import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreateInventoryMovementRequest, InventoryItem, InventoryMovementType } from '../../../core/api/inventory.models';

@Component({ selector: 'app-inventory-movement-dialog', imports: [CommonModule, ReactiveFormsModule], templateUrl: './inventory-movement-dialog.html' })
export class InventoryMovementDialog {
  @Input({ required: true }) item!: InventoryItem;
  @Input() allowNegativeStock = false; @Input() saving = false; @Input() error = '';
  @Output() closed = new EventEmitter<void>(); @Output() confirmed = new EventEmitter<CreateInventoryMovementRequest>();
  readonly form = new FormGroup({ type: new FormControl<InventoryMovementType>('StockEntry', { nonNullable: true, validators: Validators.required }), quantity: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(.001)] }), reason: new FormControl('', { nonNullable: true }) });
  get delta() { return ['StockExit', 'NegativeAdjustment'].includes(this.form.controls.type.value) ? -this.form.controls.quantity.value : this.form.controls.quantity.value; }
  get projected() { return this.item.quantity + this.delta; }
  get invalid() { const adjustment = this.form.controls.type.value.includes('Adjustment'); return this.form.invalid || (adjustment && !this.form.controls.reason.value.trim()) || (!this.allowNegativeStock && this.projected < 0); }
  submit() { if (!this.invalid) this.confirmed.emit(this.form.getRawValue()); }
}
