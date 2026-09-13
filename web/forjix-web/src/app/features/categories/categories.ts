import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { apiError } from '../../core/api/api-error';
import { ManagementApiService } from '../../core/api/management-api.service';
import { CategoryItem } from '../../core/api/management.models';
import { AuthSessionStore } from '../../core/auth/auth-session.store';

@Component({ selector: 'app-categories', imports: [CommonModule, ReactiveFormsModule], templateUrl: './categories.html' })
export class Categories {
  private readonly api = inject(ManagementApiService); private readonly session = inject(AuthSessionStore);
  protected readonly categories = signal<CategoryItem[]>([]); protected readonly modal = signal(false); protected readonly editing = signal<CategoryItem | null>(null); protected readonly error = signal('');
  protected readonly canManage = this.session.context()?.permissions.includes('categories.manage') ?? false;
  protected readonly form = new FormGroup({ name: new FormControl('', { nonNullable: true, validators: Validators.required }), description: new FormControl('', { nonNullable: true }), isActive: new FormControl(true, { nonNullable: true }) });
  constructor() { this.load(); }
  protected load() { this.api.categories().subscribe(items => this.categories.set(items)); }
  protected open(item?: CategoryItem) { this.editing.set(item ?? null); this.error.set(''); this.form.reset({ name: item?.name ?? '', description: item?.description ?? '', isActive: item?.isActive ?? true }); this.modal.set(true); }
  protected save() { if (this.form.invalid) return; const request = this.form.getRawValue(); const call = this.editing() ? this.api.updateCategory(this.editing()!.id, request) : this.api.createCategory(request); call.subscribe({ next: () => { this.modal.set(false); this.load(); }, error: err => this.error.set(apiError(err)) }); }
  protected deactivate(item: CategoryItem) { if (!confirm(`Desativar a categoria ${item.name}?`)) return; this.api.deactivateCategory(item.id).subscribe({ next: () => this.load(), error: err => alert(apiError(err)) }); }
}
