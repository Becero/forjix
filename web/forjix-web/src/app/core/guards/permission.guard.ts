import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthSessionStore } from '../auth/auth-session.store';

export const permissionGuard: CanActivateFn = route => {
  const permission = route.data['permission'] as string | undefined;
  const allowed = !permission || inject(AuthSessionStore).context()?.permissions.includes(permission);
  return allowed ? true : inject(Router).createUrlTree(['/app']);
};
