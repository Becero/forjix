import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthSessionStore } from '../auth/auth-session.store';

export const authGuard: CanActivateFn = () => {
  const session = inject(AuthSessionStore);
  return session.isAuthenticated() || inject(Router).createUrlTree(['/login']);
};

