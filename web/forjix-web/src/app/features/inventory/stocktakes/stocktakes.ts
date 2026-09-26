import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { StockApiService, Stocktake, stocktakeLabels } from '../../../core/api/stock-api.service';
import { InventoryApiService } from '../../../core/api/inventory-api.service';
import { InventoryItem } from '../../../core/api/inventory.models';
import { AuthSessionStore } from '../../../core/auth/auth-session.store';
import { apiError } from '../../../core/api/api-error';
@Component({selector:'app-stocktakes',imports:[CommonModule,ReactiveFormsModule],templateUrl:'./stocktakes.html'})
export class Stocktakes {
  private readonly api=inject(StockApiService); private readonly stock=inject(InventoryApiService); private readonly session=inject(AuthSessionStore);
  readonly labels=stocktakeLabels; readonly items=signal<Stocktake[]>([]); readonly products=signal<InventoryItem[]>([]); readonly selected=signal<Stocktake|null>(null); readonly edit=signal<Stocktake|null>(null);
  readonly editor=signal(false); readonly loading=signal(false); readonly saving=signal(false); readonly error=signal(''); readonly success=signal(''); readonly total=signal(0); readonly page=signal(1);
  readonly productSearch=new FormControl('',{nonNullable:true}); readonly countSearch=new FormControl('',{nonNullable:true}); readonly reason=new FormControl('',{nonNullable:true});
  readonly action=signal(''); readonly checked=signal<string[]>([]);
  readonly filters=new FormGroup({search:new FormControl('',{nonNullable:true}),status:new FormControl('',{nonNullable:true}),from:new FormControl('',{nonNullable:true}),to:new FormControl('',{nonNullable:true})});
  readonly form=new FormGroup({notes:new FormControl('',{nonNullable:true,validators:Validators.maxLength(2000)})});
  readonly counts=new FormArray<FormGroup<{quantity:FormControl<number|null>;notes:FormControl<string>}>>([]);
  constructor(){this.load(); if(this.can('stock.view'))this.stock.list({}).subscribe({next:x=>this.products.set(x),error:e=>this.error.set(apiError(e))});}
  can(p:string){return this.session.context()?.permissions.includes(p)??false;}
  filteredProducts(){const q=this.productSearch.value.toLowerCase(); return this.products().filter(x=>(x.productName+' '+x.sku).toLowerCase().includes(q));}
  matches(text:string){return text.toLowerCase().includes(this.countSearch.value.toLowerCase());}
  toggle(id:string){this.checked.update(ids=>ids.includes(id)?ids.filter(x=>x!==id):[...ids,id]);}
  load(page=1){this.page.set(page);this.loading.set(true);this.error.set('');const f=this.filters.getRawValue();this.api.list({...f,from:f.from?f.from+'T00:00:00Z':'',to:f.to?f.to+'T23:59:59.9999999Z':'',page,pageSize:20}).subscribe({next:x=>{this.items.set(x.items);this.total.set(x.total);this.loading.set(false);},error:e=>{this.error.set(apiError(e));this.loading.set(false);}});}
  openEditor(item:Stocktake|null=null){this.edit.set(item);this.checked.set(item?.items.map(x=>x.productId)??[]);this.form.reset({notes:item?.notes??''});this.editor.set(true);this.error.set('');}
  save(){if(this.form.invalid||!this.checked().length){this.error.set('Selecione produtos e revise a observação.');return;}this.saving.set(true);this.api.save(this.edit()?.id??null,{productIds:this.checked(),notes:this.form.controls.notes.value,rowVersion:this.edit()?.rowVersion}).subscribe({next:x=>{this.saving.set(false);this.editor.set(false);this.show(x);this.success.set('Inventário salvo sem alterar estoque.');this.load();},error:e=>this.fail(e)});}
  view(item:Stocktake){this.loading.set(true);this.api.get(item.id).subscribe({next:x=>{this.show(x);this.loading.set(false);},error:e=>{this.error.set(apiError(e));this.loading.set(false);}});}
  show(item:Stocktake){this.selected.set(item);this.countSearch.setValue('');this.counts.clear();item.items.forEach(x=>this.counts.push(new FormGroup({quantity:new FormControl<number|null>(x.countedQuantity,{validators:Validators.min(0)}),notes:new FormControl(x.notes??'',{nonNullable:true,validators:Validators.maxLength(500)})}))); }
  difference(index:number){const q=this.counts.at(index).controls.quantity.value;return q===null?null:q-(this.selected()?.items[index].expectedQuantity??0);}
  canAction(item:Stocktake,action:string){return action==='start'?item.status==='Draft'&&this.can('stock.inventory.count'):action==='complete'?item.status==='Counting'&&item.items.every(x=>x.countedQuantity!==null)&&!(this.selected()?.id===item.id&&this.counts.dirty)&&this.can('stock.inventory.complete'):['Draft','Counting'].includes(item.status)&&this.can('stock.inventory.cancel');}
  confirm(action:string){this.action.set(action);this.reason.setValue('');}
  execute(){const item=this.selected();if(!item)return;if(this.action()==='cancel'&&!this.reason.value.trim()){this.error.set('Informe o motivo do cancelamento.');return;}this.saving.set(true);this.api.action(item,this.action(),this.reason.value).subscribe({next:x=>{this.saving.set(false);this.action.set('');this.show(x);this.success.set('Operação concluída.');this.load();},error:e=>this.fail(e)});}
  recount(index:number){this.counts.at(index).controls.quantity.setValue(null);this.counts.at(index).controls.quantity.markAsDirty();}
  saveCounts(){const item=this.selected();if(!item||this.counts.invalid)return;const values=this.counts.getRawValue().flatMap((x,i)=>x.quantity===null||!this.counts.at(i).dirty?[]:[{productId:item.items[i].productId,quantity:x.quantity,notes:x.notes}]);if(!values.length){this.error.set('Informe ao menos uma contagem.');return;}this.saving.set(true);this.api.count(item,values).subscribe({next:x=>{this.saving.set(false);this.show(x);this.success.set('Contagens registradas. Estoque não alterado.');this.load();},error:e=>this.fail(e)});}
  private fail(e:unknown){this.saving.set(false);this.error.set(apiError(e));}
}
