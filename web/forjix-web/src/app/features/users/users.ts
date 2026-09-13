import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ManagementApiService } from '../../core/api/management-api.service';
import { RoleItem, UserItem } from '../../core/api/management.models';
import { apiError } from '../../core/api/api-error';
import { AuthSessionStore } from '../../core/auth/auth-session.store';

@Component({ selector: 'app-users', imports: [CommonModule, ReactiveFormsModule], templateUrl: './users.html' })
export class Users {
  private readonly api = inject(ManagementApiService);
  private readonly session = inject(AuthSessionStore);
  protected readonly users = signal<UserItem[]>([]);
  protected readonly roles = signal<RoleItem[]>([]);
  protected readonly modal = signal(false);
  protected readonly editing = signal<UserItem | null>(null);
  protected readonly error = signal('');
  protected readonly canManage = this.session.context()?.permissions.includes('administration.users.manage') ?? false;
  protected readonly form = new FormGroup({ name: new FormControl('', { nonNullable: true, validators: Validators.required }), email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }), password: new FormControl('', { nonNullable: true }), isActive: new FormControl(true, { nonNullable: true }), roleIds: new FormControl<string[]>([], { nonNullable: true }) });

  constructor() { this.load(); this.api.roles().subscribe(items => this.roles.set(items)); }
  protected load() { this.api.users().subscribe(items => this.users.set(items)); }
  protected open(user?: UserItem) { this.editing.set(user ?? null); this.error.set(''); this.form.reset({ name: user?.name ?? '', email: user?.email ?? '', password: '', isActive: user?.isActive ?? true, roleIds: user?.groups.map(x => x.id) ?? [] }); this.modal.set(true); }
  protected selected(id: string) { return this.form.controls.roleIds.value.includes(id); }
  protected toggleRole(id: string) { const ids = this.form.controls.roleIds.value; this.form.controls.roleIds.setValue(ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id]); }
  protected save() { if (this.form.invalid || (!this.editing() && this.form.controls.password.value.length < 12)) { this.error.set('Preencha os campos obrigatórios. A senha inicial deve ter 12 caracteres.'); return; } const request = this.form.getRawValue(); const call = this.editing() ? this.api.updateUser(this.editing()!.id, request) : this.api.createUser(request); call.subscribe({ next: () => { this.modal.set(false); this.load(); }, error: err => this.error.set(apiError(err)) }); }
}
