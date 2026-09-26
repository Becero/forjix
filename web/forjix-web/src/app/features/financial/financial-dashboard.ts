import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { FinancialApiService, FinancialDashboard } from '../../core/api/financial-api.service';
import { apiError } from '../../core/api/api-error';
@Component({ selector: 'app-financial-dashboard', standalone: true, imports: [CommonModule, ReactiveFormsModule], templateUrl: './financial-dashboard.html' })
export class FinancialDashboardPage {
  private readonly api = inject(FinancialApiService);
  readonly data = signal<FinancialDashboard | null>(null); readonly loading = signal(false); readonly error = signal('');
  readonly form = new FormGroup({ from: new FormControl('', { nonNullable: true }), through: new FormControl('', { nonNullable: true }) });
  constructor() { this.period('month'); }
  period(kind: 'today'|'week'|'month') {
    const end = new Date(); const start = new Date(end);
    if (kind === 'week') start.setUTCDate(start.getUTCDate() - 6);
    if (kind === 'month') start.setUTCDate(1);
    this.form.setValue({ from: start.toISOString().slice(0, 10), through: end.toISOString().slice(0, 10) }); this.load();
  }
  load() {
    this.loading.set(true); const f = this.form.getRawValue();
    this.api.dashboard(f.from, f.through).subscribe({ next: x => { this.data.set(x); this.loading.set(false); this.error.set(''); }, error: e => { this.loading.set(false); this.error.set(apiError(e)); } });
  }
}
