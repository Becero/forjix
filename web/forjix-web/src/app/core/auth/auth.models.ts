export interface SessionUser { id: string; name: string; email: string; }
export interface SessionTenant { id: string; name: string; slug: string; }
export interface SessionContext {
  user: SessionUser;
  tenant: SessionTenant;
  roles: string[];
  permissions: string[];
  features: string[];
  settings: Record<string, unknown>;
}
export interface AuthenticationResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  context: SessionContext;
}
export interface LoginRequest { tenantSlug: string; email: string; password: string; }
