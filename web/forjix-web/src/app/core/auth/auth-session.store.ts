import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthSessionStore {
  private readonly authenticated = signal(false);
  readonly isAuthenticated = this.authenticated.asReadonly();
}

