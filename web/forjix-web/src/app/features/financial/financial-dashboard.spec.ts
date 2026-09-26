import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { FinancialApiService } from '../../core/api/financial-api.service';
import { FinancialDashboardPage } from './financial-dashboard';

describe('FinancialDashboardPage', () => {
  const result = { totalReceivable: 100, totalPayable: 40, overdueCount: 1, overdueAmount: 10, received: 20, paid: 5, balance: 15, upcoming: [] };
  let api: jasmine.SpyObj<FinancialApiService>;
  beforeEach(() => {
    api = jasmine.createSpyObj<FinancialApiService>('finance', ['dashboard']);
    api.dashboard.and.returnValue(of(result));
    TestBed.configureTestingModule({ providers: [{ provide: FinancialApiService, useValue: api }] });
  });
  const create = () => TestBed.runInInjectionContext(() => new FinancialDashboardPage());

  it('loads the current month and displays the API balance', () => {
    const page = create();
    expect(page.form.controls.from.value.endsWith('-01')).toBeTrue();
    expect(page.data()?.balance).toBe(15);
    expect(page.loading()).toBeFalse();
  });
  it('supports today and the last seven calendar days', () => {
    const page = create(); page.period('today');
    expect(page.form.controls.from.value).toBe(page.form.controls.through.value);
    page.period('week');
    const values = page.form.getRawValue();
    expect((Date.parse(values.through) - Date.parse(values.from)) / 86400000).toBe(6);
  });
  it('ends loading and reports API errors', () => {
    api.dashboard.and.returnValue(throwError(() => ({ error: { detail: 'Período inválido.' } })));
    const page = create();
    expect(page.loading()).toBeFalse();
    expect(page.error()).toBeTruthy();
  });
});
