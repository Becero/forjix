import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { StockApiService, StockMovement, StockReportItem, adjustmentReasons, stockOrigins } from '../../../core/api/stock-api.service';
import { InventoryApiService } from '../../../core/api/inventory-api.service';
import { InventoryItem } from '../../../core/api/inventory.models';
import { AuthSessionStore } from '../../../core/auth/auth-session.store';
import { apiError } from '../../../core/api/api-error';
@Component({selector:'app-stock-operations',imports:[CommonModule,ReactiveFormsModule],templateUrl:'./stock-operations.html'})
export class StockOperations {
  private readonly api=inject(StockApiService);private readonly stock=inject(InventoryApiService);private readonly session=inject(AuthSessionStore);
  readonly mode=inject(ActivatedRoute).snapshot.data['mode'] as string;readonly reasons=adjustmentReasons;readonly origins=stockOrigins;
  readonly products=signal<InventoryItem[]>([]);readonly reports=signal<StockReportItem[]>([]);readonly movements=signal<StockMovement[]>([]);readonly error=signal('');readonly success=signal('');readonly loading=signal(false);readonly saving=signal(false);readonly total=signal(0);readonly page=signal(1);
  readonly filters=new FormGroup({productId:new FormControl('',{nonNullable:true}),from:new FormControl('',{nonNullable:true}),to:new FormControl('',{nonNullable:true}),origin:new FormControl('',{nonNullable:true}),user:new FormControl('',{nonNullable:true}),type:new FormControl('',{nonNullable:true}),kind:new FormControl('low-stock',{nonNullable:true}),days:new FormControl(30,{nonNullable:true})});
  readonly form=new FormGroup({productId:new FormControl('',{nonNullable:true,validators:Validators.required}),type:new FormControl('PositiveAdjustment',{nonNullable:true}),quantity:new FormControl(1,{nonNullable:true,validators:[Validators.required,Validators.min(.001)]}),reason:new FormControl('Correction',{nonNullable:true,validators:Validators.required}),observation:new FormControl('',{nonNullable:true,validators:Validators.maxLength(500)})});
  constructor(){if(this.session.context()?.permissions.includes('stock.view'))this.stock.list({}).subscribe({next:x=>this.products.set(x),error:e=>this.error.set(apiError(e))});if(this.mode!=='adjust')this.load();}
  selected(){return this.products().find(x=>x.productId===this.form.controls.productId.value);}
  projected(){const item=this.selected();return (item?.quantity??0)+(this.form.controls.type.value==='NegativeAdjustment'?-1:1)*this.form.controls.quantity.value;}
  load(page=1){this.page.set(page);this.loading.set(true);this.error.set('');const f=this.filters.getRawValue();const range={from:f.from?f.from+'T00:00:00Z':'',to:f.to?f.to+'T23:59:59.9999999Z':''};
    if(this.mode==='reports')this.api.report(f.kind,{days:f.days,...range}).subscribe({next:x=>{this.reports.set(x);this.loading.set(false);},error:e=>this.fail(e)});
    else this.api.movements({productId:f.productId,type:f.type,origin:f.origin,user:f.user,...range,page,pageSize:50}).subscribe({next:x=>{this.movements.set(x.items);this.total.set(x.total);this.loading.set(false);},error:e=>this.fail(e)});
  }
  adjust(){const item=this.selected();if(this.form.invalid||!item){this.error.set('Selecione produto e revise os campos.');return;}this.saving.set(true);this.error.set('');this.api.adjust({...this.form.getRawValue(),rowVersion:item.rowVersion}).subscribe({next:x=>{this.products.update(items=>items.map(p=>p.productId===x.inventory.productId?x.inventory:p));this.saving.set(false);this.success.set('Ajuste registrado com sucesso.');this.form.controls.quantity.setValue(1);},error:e=>this.fail(e)});}
  private fail(e:unknown){this.error.set(apiError(e));this.loading.set(false);this.saving.set(false);}
}
