import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ManagementApiService } from '../../core/api/management-api.service';
import { AuditItem, UserItem } from '../../core/api/management.models';

@Component({ selector: 'app-audit', imports: [CommonModule, ReactiveFormsModule], templateUrl: './audit.html' })
export class Audit {
  private readonly api = inject(ManagementApiService);
  protected readonly items = signal<AuditItem[]>([]); protected readonly users = signal<UserItem[]>([]);
  protected readonly filters = new FormGroup({ userId: new FormControl('', { nonNullable: true }), action: new FormControl('', { nonNullable: true }), entity: new FormControl('', { nonNullable: true }), from: new FormControl('', { nonNullable: true }), to: new FormControl('', { nonNullable: true }) });
  constructor() { this.load(); this.api.users().subscribe(items => this.users.set(items)); }
  protected load() { const values = this.filters.getRawValue(); const filters = { ...values, from: values.from ? new Date(`${values.from}T00:00:00`).toISOString() : '', to: values.to ? new Date(`${values.to}T23:59:59`).toISOString() : '' }; this.api.audit(filters).subscribe(items => this.items.set(items)); }
  protected detail(item: AuditItem) { if (!item.details) return '—'; try { return JSON.stringify(JSON.parse(item.details)); } catch { return '—'; } }
}
