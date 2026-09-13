import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NgIf } from '@angular/common';
import { AuthService } from '../../core/auth/auth.service';
import { AuthSessionStore } from '../../core/auth/auth-session.store';

@Component({
  selector: 'app-authenticated-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgIf],
  templateUrl: './authenticated-layout.html',
  styleUrl: './authenticated-layout.scss'
})
export class AuthenticatedLayout {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly context = inject(AuthSessionStore).context;

  protected can(permission: string): boolean { return this.context()?.permissions.includes(permission) ?? false; }

  protected logout(): void {
    this.auth.logout().subscribe(() => void this.router.navigateByUrl('/login'));
  }
}
