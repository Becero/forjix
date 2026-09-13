import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { apiError } from '../../core/api/api-error';
import { ManagementApiService } from '../../core/api/management-api.service';
import { CategoryItem, ProductItem } from '../../core/api/management.models';
import { AuthSessionStore } from '../../core/auth/auth-session.store';
import { RouterLink } from '@angular/router';

@Component({ selector: 'app-products', imports: [CommonModule, ReactiveFormsModule, RouterLink], templateUrl: './products.html' })
export class Products {
  private readonly api = inject(ManagementApiService); private readonly session = inject(AuthSessionStore);
  protected readonly products = signal<ProductItem[]>([]); protected readonly categories = signal<CategoryItem[]>([]); protected readonly modal = signal(false); protected readonly editing = signal<ProductItem | null>(null); protected readonly error = signal('');
  protected readonly canManage = this.session.context()?.permissions.includes('products.manage') ?? false;
  protected readonly canViewCategories = this.session.context()?.permissions.includes('categories.view') ?? false;
  protected readonly filters = new FormGroup({ search: new FormControl('', { nonNullable: true }), categoryId: new FormControl('', { nonNullable: true }), isActive: new FormControl('', { nonNullable: true }) });
  protected readonly form = new FormGroup({ categoryId: new FormControl('', { nonNullable: true, validators: Validators.required }), name: new FormControl('', { nonNullable: true, validators: Validators.required }), sku: new FormControl('', { nonNullable: true, validators: Validators.required }), barcode: new FormControl('', { nonNullable: true }), salePrice: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(.01)] }), costPrice: new FormControl(0, { nonNullable: true, validators: Validators.min(0) }), minimumStock: new FormControl(0, { nonNullable: true, validators: Validators.min(0) }), isActive: new FormControl(true, { nonNullable: true }), rowVersion: new FormControl('', { nonNullable: true }) });
  constructor() {
    this.load();
    if (this.canViewCategories) this.api.categories().subscribe(items => this.categories.set(items));
  }
  protected load() { this.api.products(this.filters.getRawValue()).subscribe(items => this.products.set(items)); }
  protected open(item?: ProductItem) { this.editing.set(item ?? null); this.error.set(''); this.form.reset({ categoryId: item?.categoryId ?? '', name: item?.name ?? '', sku: item?.sku ?? '', barcode: item?.barcode ?? '', salePrice: item?.salePrice ?? 0, costPrice: item?.costPrice ?? 0, minimumStock: item?.minimumStock ?? 0, isActive: item?.isActive ?? true, rowVersion: item?.rowVersion ?? '' }); this.modal.set(true); }
  protected save() { if (this.form.invalid) { this.error.set('Revise os campos obrigatórios e os valores informados.'); return; } const request = this.form.getRawValue(); const call = this.editing() ? this.api.updateProduct(this.editing()!.id, request) : this.api.createProduct(request); call.subscribe({ next: () => { this.modal.set(false); this.load(); }, error: err => this.error.set(apiError(err)) }); }
  protected deactivate(item: ProductItem) { if (!confirm(`Desativar o produto ${item.name}?`)) return; this.api.deactivateProduct(item.id).subscribe({ next: () => this.load(), error: err => alert(apiError(err)) }); }
}
