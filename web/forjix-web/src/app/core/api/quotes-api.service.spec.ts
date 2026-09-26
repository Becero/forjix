import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { QuotesApiService } from './quotes-api.service';
describe('QuotesApiService', () => {
  let api: QuotesApiService; let http: HttpTestingController;
  beforeEach(() => { TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }); api = TestBed.inject(QuotesApiService); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());
  it('sends list filters without empty dates', () => {
    api.list({ status: 'Sent', expiredOnly: false, page: 2, from: '' }).subscribe();
    const request = http.expectOne(r => r.url === '/api/quotes');
    expect(request.request.params.get('page')).toBe('2'); expect(request.request.params.has('from')).toBeFalse(); request.flush({ items: [], total: 0 });
  });
  it('creates without trusting client totals', () => {
    api.save({ customerId: 'customer', validUntil: '2026-10-10', discount: 0, notes: '', items: [{ productId: 'product', quantity: 2, discount: 0 }] }).subscribe();
    const request = http.expectOne('/api/quotes'); expect(request.request.method).toBe('POST'); expect(request.request.body.total).toBeUndefined(); request.flush({});
  });
  it('uses the current version for approval', () => {
    api.action('quote', 'approve', 'version').subscribe();
    const request = http.expectOne('/api/quotes/quote/approve'); expect(request.request.body.rowVersion).toBe('version'); request.flush({});
  });
  it('passes deferred terms to the existing sale conversion', () => {
    api.convert('quote', 'version', 'Deferred', { financialCategoryId: 'income', firstDueDate: '2026-10-10', installments: 2 }).subscribe();
    const request = http.expectOne('/api/quotes/quote/convert-to-sale');
    expect(request.request.body.financialTerms.installments).toBe(2); expect(request.request.body.paymentMethod).toBe('Deferred'); request.flush({});
  });
  it('downloads PDF as a binary blob', () => {
    api.pdf('quote').subscribe();
    const request = http.expectOne('/api/quotes/quote/pdf'); expect(request.request.responseType).toBe('blob'); request.flush(new Blob(['%PDF']));
  });
});
