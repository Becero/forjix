import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { AuthSessionStore } from './auth-session.store';
import { AuthenticationResponse, LoginRequest, SessionContext } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly store = inject(AuthSessionStore);
  private refreshRequest?: Observable<AuthenticationResponse>;

  login(request: LoginRequest): Observable<SessionContext> {
    return this.http.post<AuthenticationResponse>('/api/auth/login', request, { withCredentials: true }).pipe(
      tap(session => this.store.setSession(session)), map(session => session.context));
  }

  refresh(): Observable<AuthenticationResponse> {
    if (!this.refreshRequest) {
      this.refreshRequest = this.http.post<AuthenticationResponse>('/api/auth/refresh', {}, { withCredentials: true }).pipe(
        tap(session => this.store.setSession(session)),
        finalize(() => this.refreshRequest = undefined),
        shareReplay({ bufferSize: 1, refCount: false }));
    }
    return this.refreshRequest;
  }

  ensureSession(): Observable<boolean> {
    if (this.store.isAuthenticated()) return of(true);
    return this.refresh().pipe(map(() => true), catchError(() => { this.store.clear(); return of(false); }));
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', {}, { withCredentials: true }).pipe(
      catchError(() => of(undefined)), tap(() => this.store.clear()));
  }
}
