import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { InventoryList } from './inventory-list';
import { StockApiService } from '../../../core/api/stock-api.service';
import { InventoryApiService } from '../../../core/api/inventory-api.service';
import { ManagementApiService } from '../../../core/api/management-api.service';
import { AuthSessionStore } from '../../../core/auth/auth-session.store';
describe('Inventory overview',()=>{
 beforeEach(()=>TestBed.configureTestingModule({providers:[{provide:StockApiService,useValue:{overview:()=>of({controlledProducts:10,outOfStock:3,lowStock:2,nearMinimum:1,ongoingInventories:1,estimatedValue:100})}},{provide:InventoryApiService,useValue:{list:()=>of([])}},{provide:ManagementApiService,useValue:{}},{provide:AuthSessionStore,useValue:{context:signal({permissions:['stock.view','stock.manage']})}}]}));
 it('loads stock indicators without needing products or dashboard permission',()=>{const p=TestBed.runInInjectionContext(()=>new InventoryList());expect(p.stockOverview()?.outOfStock).toBe(3);expect(p.stockOverview()?.ongoingInventories).toBe(1);});
 it('does not give adjustment access just because stock.manage is present',()=>{const p=TestBed.runInInjectionContext(()=>new InventoryList());expect(p.canManage).toBeTrue();expect(p.canAdjust).toBeFalse();});
});
