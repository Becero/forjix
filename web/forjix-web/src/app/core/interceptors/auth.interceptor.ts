import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { AuthSessionStore } from '../auth/auth-session.store';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const store = inject(AuthSessionStore);
  const auth = inject(AuthService);
  const isAuthRequest = request.url.includes('/api/auth/');
  const token = store.accessToken();
  const authorizedRequest = token && !isAuthRequest
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` }, withCredentials: true })
    : request.clone({ withCredentials: true });

  return next(authorizedRequest).pipe(catchError(error => {
    if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isAuthRequest) return throwError(() => error);
    return auth.refresh().pipe(
      switchMap(() => next(request.clone({ setHeaders: { Authorization: `Bearer ${store.accessToken()}` }, withCredentials: true }))),
      catchError(refreshError => { store.clear(); return throwError(() => refreshError); }));
  }));
};
