# Autenticação e autorização

## Login

`POST /api/auth/login` recebe `LoginRequest(TenantSlug, Email, Password)`. `AuthenticationService` resolve o tenant por slug, normaliza email com trim/uppercase e consulta o usuário incluindo grupos/permissões. Tenant inexistente/inválido, usuário ausente, inativo, bloqueado ou senha incorreta resultam na mesma resposta 401.

Em sucesso:

- access token JWT é emitido;
- refresh token aleatório de 64 bytes é criado;
- somente SHA-256 hexadecimal do refresh é salvo;
- `AuditLog(LoginSucceeded)` e token são persistidos;
- token bruto fica dentro de cookie protegido, nunca na resposta JSON.

## JWT

Assinatura HMAC-SHA256. Valida issuer, audience, assinatura, tempo de vida e clock skew. `JwtOptions` exige signing key com pelo menos 32 bytes, access token entre 1–60 minutos e skew entre 0–300 segundos.

| Claim | Conteúdo |
| --- | --- |
| `sub` e `user_id` | GUID do usuário |
| `tenant_id` | GUID do tenant |
| `tenant_slug` | slug validado |
| `jti` | identificador aleatório do token |
| `role` | uma claim por nome de grupo |

O padrão de expiração é 15 minutos. Permissões não entram no JWT; são consultadas no banco a cada autorização.

## Refresh cookie

- Desenvolvimento: `forjix.refresh`; demais ambientes: `__Secure-forjix-refresh`.
- `HttpOnly=true`, `SameSite=Strict`, `Secure=true` fora de Development, path `/api/auth`, expiração explícita e `IsEssential=true`.
- Conteúdo: `RefreshCookiePayload(TenantId, Token)` serializado e protegido por ASP.NET Data Protection.
- Validade lógica do refresh: 7 dias.

`POST /refresh` localiza o hash, rejeita expirado/inválido, valida usuário e rotaciona o token com rowversion. Reutilizar token já revogado revoga a família. `POST /logout` revoga o token encontrado e remove cookie.

## Senhas

`PasswordHasher<User>` de ASP.NET Identity gera/verifica hashes. Criação de usuário e seeds exigem no mínimo 12 caracteres; `LoginRequest` aceita formalmente 8–200 na validação HTTP, mas usuários criados pela aplicação usam o mínimo de 12. Hashes, e não senhas, ficam em `Users`.

## Roles, permissions e policies

Usuários e roles são N:N; roles e permissions são N:N. `Permissions.Catalog` contém 29 códigos. `RequirePermissionAttribute` cria policy `Permission:<code>`. `PermissionPolicyProvider` gera a policy dinamicamente e exige usuário autenticado. `PermissionAuthorizationHandler` extrai tenant/user das claims e chama `PermissionChecker`, que consulta o banco correto.

Permissões atuais:

```text
dashboard.view
administration.users.view | administration.users.manage
administration.roles.view | administration.roles.manage
audit.view
products.view | products.manage
categories.view | categories.manage
stock.view | stock.manage
sales.view | sales.create | sales.cancel | sales.discount
reports.view | reports.export
customers.view | customers.manage
suppliers.view | suppliers.manage
purchases.view | purchases.manage | purchases.receive
cash.view | cash.manage
settings.view | settings.manage
```

## Frontend

O access token e contexto ficam apenas em memória. `authInterceptor` envia bearer e tenta uma renovação compartilhada ao receber 401. `authGuard` tenta restaurar a sessão via cookie. `permissionGuard`, menu e botões ocultam acesso sem permissão, mas não substituem policies server-side.

## Auditoria de identidade

Login com sucesso/falha, rotação/revogação e logout geram ações de auditoria. O audit registra correlation ID e instante; IP é preenchido em operações que usam `ICurrentUser`, enquanto o helper atual de autenticação não define `IpAddress`.
