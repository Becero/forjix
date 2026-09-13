import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { apiError } from '../../core/api/api-error';
import { ManagementApiService } from '../../core/api/management-api.service';
import { PermissionItem, RoleItem } from '../../core/api/management.models';
import { AuthSessionStore } from '../../core/auth/auth-session.store';

@Component({ selector: 'app-roles', imports: [CommonModule, ReactiveFormsModule], templateUrl: './roles.html' })
export class Roles {
  private readonly api = inject(ManagementApiService); private readonly session = inject(AuthSessionStore);
  protected readonly roles = signal<RoleItem[]>([]); protected readonly permissions = signal<PermissionItem[]>([]); protected readonly modal = signal(false); protected readonly editing = signal<RoleItem | null>(null); protected readonly error = signal('');
  protected readonly modules = computed(() => [...new Set(this.permissions().map(x => x.module))]);
  protected readonly canManage = this.session.context()?.permissions.includes('administration.roles.manage') ?? false;
  protected readonly form = new FormGroup({ name: new FormControl('', { nonNullable: true, validators: Validators.required }), description: new FormControl('', { nonNullable: true }), permissions: new FormControl<string[]>([], { nonNullable: true }) });
  constructor() { this.load(); this.api.permissions().subscribe(items => this.permissions.set(items)); }
  protected load() { this.api.roles().subscribe(items => this.roles.set(items)); }
  protected byModule(module: string) { return this.permissions().filter(x => x.module === module); }
  protected open(role?: RoleItem) { this.editing.set(role ?? null); this.error.set(''); this.form.reset({ name: role?.name ?? '', description: role?.description ?? '', permissions: role?.permissions ?? [] }); this.modal.set(true); }
  protected selected(code: string) { return this.form.controls.permissions.value.includes(code); }
  protected toggle(code: string) { const values = this.form.controls.permissions.value; this.form.controls.permissions.setValue(values.includes(code) ? values.filter(x => x !== code) : [...values, code]); }
  protected save() { if (this.form.invalid) return; const request = this.form.getRawValue(); const call = this.editing() ? this.api.updateRole(this.editing()!.id, request) : this.api.createRole(request); call.subscribe({ next: () => { this.modal.set(false); this.load(); }, error: err => this.error.set(apiError(err)) }); }
}
