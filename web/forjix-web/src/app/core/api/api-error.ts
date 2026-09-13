import { HttpErrorResponse } from '@angular/common/http';

export function apiError(error: unknown): string {
  const response = error as HttpErrorResponse;
  return response.error?.detail || response.error?.title || 'Não foi possível concluir a operação.';
}
