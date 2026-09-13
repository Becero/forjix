import { HttpInterceptorFn } from '@angular/common/http';

export const correlationIdInterceptor: HttpInterceptorFn = (request, next) => {
  const correlationId = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`;
  return next(request.clone({ setHeaders: { 'X-Correlation-ID': correlationId } }));
};

