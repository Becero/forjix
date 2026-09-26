import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { StockApiService, Stocktake } from './stock-api.service';
describe('StockApiService',()=>{
 let api:StockApiService;let http:HttpTestingController;
 const item={id:'id',rowVersion:'version'} as Stocktake;
 beforeEach(()=>{TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});api=TestBed.inject(StockApiService);http=TestBed.inject(HttpTestingController);});
 afterEach(()=>http.verify());
 it('loads tenant scoped overview',()=>{api.overview().subscribe(value=>expect(value.controlledProducts).toBe(3));const r=http.expectOne('/api/stock/overview');expect(r.request.method).toBe('GET');r.flush({controlledProducts:3});});
 it('sends inventory pagination and omits empty filters',()=>{api.list({search:'INV-',status:'',page:2}).subscribe();const r=http.expectOne(x=>x.url==='/api/inventories');expect(r.request.params.get('page')).toBe('2');expect(r.request.params.has('status')).toBeFalse();r.flush({items:[],total:0});});
 it('creates a stocktake without stock totals',()=>{api.save(null,{productIds:['p'],notes:'nota'}).subscribe();const r=http.expectOne('/api/inventories');expect(r.request.method).toBe('POST');expect(r.request.body).toEqual({productIds:['p'],notes:'nota'});r.flush({});});
 it('edits with current row version',()=>{api.save('id',{productIds:['p'],notes:'',rowVersion:'v'}).subscribe();const r=http.expectOne('/api/inventories/id');expect(r.request.method).toBe('PUT');expect(r.request.body.rowVersion).toBe('v');r.flush({});});
 it('sends counts in a single batch',()=>{api.count(item,[{productId:'p',quantity:0,notes:''}]).subscribe();const r=http.expectOne('/api/inventories/id/count');expect(r.request.body.items.length).toBe(1);expect(r.request.body.rowVersion).toBe('version');r.flush({});});
 it('finalizes with concurrency version',()=>{api.action(item,'complete').subscribe();const r=http.expectOne('/api/inventories/id/complete');expect(r.request.body.rowVersion).toBe('version');r.flush({});});
 it('sends adjustment reason and stock version',()=>{api.adjust({productId:'p',type:'NegativeAdjustment',quantity:1,reason:'Loss',observation:'Nota',rowVersion:'v'}).subscribe();const r=http.expectOne('/api/stock/adjustments');expect(r.request.body.reason).toBe('Loss');expect(r.request.body.rowVersion).toBe('v');r.flush({});});
 it('filters reports and movement origins',()=>{api.report('no-movement',{days:60}).subscribe();const report=http.expectOne('/api/stock/reports/no-movement?days=60');expect(report.request.params.get('days')).toBe('60');report.flush([]);api.movements({origin:'Stocktake'}).subscribe();const history=http.expectOne('/api/stock/movements?origin=Stocktake');expect(history.request.params.get('origin')).toBe('Stocktake');history.flush({items:[],total:0});});
});
