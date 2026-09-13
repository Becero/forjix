import { Injectable, computed, signal } from '@angular/core';
import { AuthenticationResponse, SessionContext } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthSessionStore {
  private readonly accessTokenState = signal<string | null>(null);
  private readonly contextState = signal<SessionContext | null>(null);

  readonly accessToken = this.accessTokenState.asReadonly();
  readonly context = this.contextState.asReadonly();
  readonly isAuthenticated = computed(() => Boolean(this.accessTokenState() && this.contextState()));

  setSession(session: AuthenticationResponse): void {
    this.accessTokenState.set(session.accessToken);
    this.contextState.set(session.context);
  }

  clear(): void {
    this.accessTokenState.set(null);
    this.contextState.set(null);
  }
}
