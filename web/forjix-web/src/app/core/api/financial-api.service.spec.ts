import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FinancialApiService, PaymentRequest } from './financial-api.service';

describe('FinancialApiService', () => {
  let service: FinancialApiService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(FinancialApiService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('passes filters and pagination without sending empty dates', () => {
    service.list('accounts-receivable', { search: 'Venda', from: '', page: 2, overdueOnly: true }).subscribe();
    const request = http.expectOne(r => r.url === '/api/financial/accounts-receivable');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('overdueOnly')).toBe('true');
    expect(request.request.params.has('from')).toBeFalse();
    request.flush({ items: [], total: 0 });
  });

  it('sends the retry key and numeric monetary values for payments', () => {
    const value: PaymentRequest = { amount: 12.34, paymentDate: '2026-09-26', paymentMethod: 'Pix', discount: 0, interest: 0, penalty: 0, notes: '', rowVersion: 'AAAAAAAAAAE=' };
    service.pay('accounts-payable', 'account', value, 'stable-retry-key').subscribe();
    const request = http.expectOne('/api/financial/accounts-payable/account/payments');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('stable-retry-key');
    expect(request.request.body.amount).toBe(12.34);
    expect(request.request.body.rowVersion).toBe(value.rowVersion);
    request.flush({});
  });

  it('uses the payment version and explicit reason for reversals', () => {
    service.reverse('accounts-receivable', 'account', 'payment', 'Correção', 'version').subscribe();
    const request = http.expectOne('/api/financial/accounts-receivable/account/payments/payment/reverse');
    expect(request.request.body).toEqual({ reason: 'Correção', rowVersion: 'version' });
    request.flush({});
  });

  it('filters financial categories by the compatible type', () => {
    service.categories('Expense').subscribe();
    const request = http.expectOne('/api/financial/categories?type=Expense');
    expect(request.request.method).toBe('GET'); request.flush([]);
  });

  it('passes the dashboard custom period', () => {
    service.dashboard('2026-09-01', '2026-09-26').subscribe();
    const request = http.expectOne('/api/financial/dashboard?from=2026-09-01&through=2026-09-26');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });
});
