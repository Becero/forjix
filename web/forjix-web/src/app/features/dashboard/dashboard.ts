import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIf } from '@angular/common';
import { AuthSessionStore } from '../../core/auth/auth-session.store';

@Component({ selector: 'app-dashboard', imports: [RouterLink, NgIf], template: `<section class="page"><div class="page-heading"><div><span class="eyebrow">VISÃO GERAL</span><h1>Painel</h1><p>Administre o catálogo e os acessos da sua empresa em um só lugar.</p></div></div><div class="metric-grid"><a *ngIf="can('products.view')" class="metric-card" routerLink="/app/products"><span>CATÁLOGO</span><strong>Produtos</strong><small>Cadastros, preços e categorias</small></a><a *ngIf="can('administration.users.view')" class="metric-card" routerLink="/app/users"><span>ADMINISTRAÇÃO</span><strong>Usuários</strong><small>Acessos e grupos por pessoa</small></a><a *ngIf="can('audit.view')" class="metric-card" routerLink="/app/audit"><span>SEGURANÇA</span><strong>Auditoria</strong><small>Histórico das operações</small></a></div><div class="content-card welcome"><span class="eyebrow">FUNDAÇÃO COMERCIAL</span><h2>Seu ambiente está pronto para operar.</h2><p>Comece cadastrando categorias e produtos. Estoque, frente de caixa, vendas e relatórios permanecem sinalizados para as próximas fases.</p></div></section>` })
export class Dashboard {
  private readonly context = inject(AuthSessionStore).context;
  protected can(permission: string) { return this.context()?.permissions.includes(permission) ?? false; }
}
