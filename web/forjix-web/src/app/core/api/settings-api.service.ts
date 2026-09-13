import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
export interface TenantSettings { tradeName:string; legalName?:string; cnpj?:string; phone?:string; email?:string; address?:string; allowNegativeStock:boolean; currency:string; timeZone:string; hasLogo:boolean }
export type UpdateTenantSettings = Omit<TenantSettings, 'hasLogo'>;
@Injectable({ providedIn:'root' })
export class SettingsApiService {
  private readonly http=inject(HttpClient);
  get(){return this.http.get<TenantSettings>('/api/settings');}
  save(value:UpdateTenantSettings){return this.http.put<TenantSettings>('/api/settings',value);}
  logo(file:File){const body=new FormData();body.append('file',file);return this.http.post('/api/settings/logo',body);}
}
