import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { Quotes } from './quotes';
import { Quote, QuotesApiService } from '../../core/api/quotes-api.service';
import { AuthSessionStore } from '../../core/auth/auth-session.store';
describe('Quotes', () => {
  let api: jasmine.SpyObj<QuotesApiService>; let permissions: string[];
  const quote: Quote = { id: 'quote', number: 'ORC-000001', customerId: 'customer', customerName: 'Cliente', issueDate: '2026-09-26', validUntil: '2026-10-26', status: 'Approved', subtotal: 20, discount: 2, total: 17, userId: 'user', userName: 'Vendedor', rowVersion: 'version', items: [{ id: 'item', productId: 'product', productName: 'Produto', sku: 'P', quantity: 2, unitPrice: 10, discount: 1, total: 19 }] };
  beforeEach(() => {
    permissions = ['quotes.view', 'quotes.create', 'quotes.edit', 'quotes.status', 'quotes.cancel', 'quotes.convert', 'sales.create'];
    api = jasmine.createSpyObj<QuotesApiService>('quotes', ['list', 'save', 'get', 'action', 'convert', 'pdf']);
    api.list.and.returnValue(of({ items: [quote], total: 1 })); api.save.and.returnValue(of(quote)); api.get.and.returnValue(of(quote)); api.action.and.returnValue(of(quote)); api.convert.and.returnValue(of({ id: 'sale', number: 'VD-EXEMPLO' }));
    TestBed.configureTestingModule({ providers: [{ provide: QuotesApiService, useValue: api }, { provide: AuthSessionStore, useValue: { context: signal({ permissions }) } }] });
  });
  const create = () => TestBed.runInInjectionContext(() => new Quotes());
  it('loads the list and finishes loading', () => { const page = create(); expect(page.items().length).toBe(1); expect(page.loading()).toBeFalse(); });
  it('opens existing data and sends only item quantities and discounts', () => {
    const page = create(); page.open(quote); expect(page.preview()).toBe(17); page.save();
    const value = api.save.calls.mostRecent().args[0]; expect(value.rowVersion).toBe('version'); expect(value.items[0]).toEqual({ productId: 'product', quantity: 2, discount: 1 });
  });
  it('requires a customer and at least one item', () => { const page = create(); page.open(); page.save(); expect(api.save).not.toHaveBeenCalled(); expect(page.error()).toBeTruthy(); });
  it('hides invalid transitions and duplicate conversion', () => {
    const page = create(); expect(page.canAction(quote, 'convert')).toBeTrue(); expect(page.canAction({ ...quote, status: 'Converted' }, 'convert')).toBeFalse(); expect(page.canAction(quote, 'approve')).toBeFalse();
  });
  it('requires a cancellation reason', () => { const page = create(); page.confirm(quote, 'cancel'); page.execute(); expect(api.action).not.toHaveBeenCalled(); });
  it('converts with the quote version and shows the sale number', () => {
    const page = create(); page.confirm(quote, 'convert'); page.execute(); expect(api.convert).toHaveBeenCalledWith('quote', 'version', 'Pix', undefined); expect(page.success()).toContain('VD-EXEMPLO');
  });
  it('reports failures without hiding the pending action', () => {
    api.convert.and.returnValue(throwError(() => ({ error: { detail: 'Sem estoque.' } })));
    const page = create(); page.confirm(quote, 'convert'); page.execute(); expect(page.error()).toBe('Sem estoque.'); expect(page.choice()).not.toBeNull(); expect(page.saving()).toBeFalse();
  });
});
