import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { InventoryMovementResult } from './inventory.models';
export interface StockOverview { controlledProducts: number; outOfStock: number; lowStock: number; nearMinimum: number; ongoingInventories: number; estimatedValue: number; }
export interface StockReportItem { productId: string; productName: string; sku: string; quantity: number; minimumStock: number; suggestedReplacement: number; lastMovementAt?: string; estimatedValue: number; }
export interface StockMovement { id: number; productId: string; productName: string; sku: string; type: string; entry: number; exit: number; previousQuantity: number; newQuantity: number; origin: string; referenceId?: string; referenceNumber?: string; userName: string; createdAt: string; reason?: string; reasonCode?: string; observation?: string; }
export interface StocktakeItem { productId: string; productName: string; sku: string; expectedQuantity: number; countedQuantity: number | null; difference: number | null; notes?: string; countedAt?: string; countedByName?: string; }
export interface Stocktake { id: string; number: string; status: string; notes?: string; userName: string; openedAt: string; startedAt?: string; closedAt?: string; rowVersion: string; items: StocktakeItem[]; }
export const stocktakeLabels: Record<string,string|undefined> = { Draft: 'Rascunho', Counting: 'Em contagem', Completed: 'Concluído', Cancelled: 'Cancelado' };
export const adjustmentReasons: Record<string,string|undefined> = { Loss: 'Perda', Damage: 'Avaria', Expiration: 'Vencimento', OperationalError: 'Erro operacional', Return: 'Devolução', Correction: 'Correção', Other: 'Outro' };
export const stockOrigins: Record<string,string|undefined> = { Sale: 'Venda', Purchase: 'Compra', Stocktake: 'Inventário', ManualAdjustment: 'Ajuste manual', DevelopmentSeed: 'Carga inicial', Other: 'Outra' };
@Injectable({providedIn:'root'})
export class StockApiService {
  private readonly http = inject(HttpClient);
  private params(values: Record<string,string|number|boolean>) { let params = new HttpParams(); Object.entries(values).filter(([,v])=>v!=='').forEach(([k,v])=>params=params.set(k,v)); return params; }
  overview() { return this.http.get<StockOverview>('/api/stock/overview'); }
  movements(filters: Record<string,string|number|boolean>) { return this.http.get<{items:StockMovement[];total:number}>('/api/stock/movements',{params:this.params(filters)}); }
  report(kind:string,filters:Record<string,string|number|boolean>) { return this.http.get<StockReportItem[]>('/api/stock/reports/'+kind,{params:this.params(filters)}); }
  adjust(request:{productId:string;type:string;quantity:number;reason:string;observation:string;rowVersion:string}) { return this.http.post<InventoryMovementResult>('/api/stock/adjustments',request); }
  list(filters:Record<string,string|number|boolean>) { return this.http.get<{items:Stocktake[];total:number}>('/api/inventories',{params:this.params(filters)}); }
  get(id:string) { return this.http.get<Stocktake>('/api/inventories/'+id); }
  save(id:string|null,request:{productIds:string[];notes:string;rowVersion?:string}) { return id ? this.http.put<Stocktake>('/api/inventories/'+id,request) : this.http.post<Stocktake>('/api/inventories',request); }
  action(item:Stocktake,action:string,reason='') { return this.http.post<Stocktake>('/api/inventories/'+item.id+'/'+action,{rowVersion:item.rowVersion,reason}); }
  count(item:Stocktake,items:{productId:string;quantity:number;notes:string}[]) { return this.http.post<Stocktake>('/api/inventories/'+item.id+'/count',{rowVersion:item.rowVersion,items}); }
}
