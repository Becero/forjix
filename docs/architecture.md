# Architecture

## Shape

Forjix starts as a modular monolith: one deployable ASP.NET Core API, one Angular application, one central platform database and one isolated operational database per tenant.

```text
Angular -> API -> Application -> Domain
                  ^             ^
                  |             |
             Infrastructure  Integrations
```

## Dependency rule

- Domain references no project.
- Application references Domain.
- Infrastructure references Application and Domain.
- Integrations references Application and Domain.
- API references Application, Infrastructure and Integrations.
- DatabaseMigrator references Infrastructure.

## Tenancy

`ForjixMaster` owns tenant, plan, subscription, feature, setting and database-location metadata. It never owns operational sales or stock data.

Each tenant database has the same schema. Operational tables do not receive a `TenantId` by default because isolation is physical. A tenant is resolved by a validated slug during login and by signed identity after authentication. The frontend cannot submit an arbitrary tenant or database identifier to switch data sources.

Connection credentials are resolved through `ISecretProvider`; `TenantDatabase.SecretReference` is the only persisted pointer.

Login is the only flow that accepts a tenant slug. The resolver requires an active tenant and a currently active subscription before opening its database. After authentication, `ICurrentUser` and `ITenantContext` read tenant identity only from validated JWT claims; a request header cannot select or replace a tenant.

## Authentication and authorization

- Passwords use the ASP.NET Core Identity password hasher.
- Access JWTs are short lived, signed by an external secret, and contain user, tenant and role identity.
- The access token is kept only in Angular memory.
- The opaque refresh token is represented in the browser by a Data Protection-encrypted, HttpOnly, SameSite cookie. Only its SHA-256 hash is stored in the tenant database.
- Refresh rotates the token, revokes the predecessor and uses a token family plus optimistic concurrency to detect replay and races.
- Permissions are loaded from the tenant database through dynamic `Permission:*` policies, avoiding role-name checks in endpoints.
- `/api/me` returns only safe user, tenant, role, permission, feature and frontend-setting context.

Explicit CORS origins and native fixed-window rate limiting protect the browser authentication boundary. Authentication failures use the same public message for invalid tenant, user or password.

## Customization

Customer differences must use plan features, tenant feature overrides, settings and adapters/providers. Customer-specific branches and conditionals are prohibited.

## Current scope

The platform currently includes functional identity and access, both DbContexts, trusted tenancy resolution, API authentication, an authenticated Angular shell, user and access-group administration, audit consultation, categories, products, inventory and atomic stock movements. Every operational request resolves the tenant from the authenticated identity.

Sales, checkout, payments, commercial reports, fiscal capabilities, external providers, mobile and multi-store behavior are not part of this delivery. Their menu entries are explicitly marked as future modules.
