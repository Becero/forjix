# Forjix engineering rules

These rules apply to the entire repository.

## Product boundaries

- This repository contains the commercial Forjix product. The former demo is only a functional and visual reference.
- Do not copy the demo architecture or code without an explicit technical review.
- Do not create branches, schemas, forks, or scattered code paths per customer.
- Never implement checks such as `if tenant == customer`.
- Customer differences must be represented by features, settings, plan limits, or pluggable providers.

## Tenancy

- The platform uses one `ForjixMaster` database and one operational database per tenant.
- Do not add `TenantId` to operational/commercial tables by default because each tenant has an isolated database.
- A client request must never choose an arbitrary TenantId, database name, server, or connection string.
- Resolve the tenant through the authenticated identity or the validated login slug/subdomain flow.
- Store only secret references in `ForjixMaster`; never store database passwords in plaintext.
- Only login may resolve an active tenant by slug. Authenticated flows must derive tenant identity from validated JWT claims.
- Create a tenant DbContext per operation after resolution; never mutate a shared DbContext connection string.

## Identity and secrets

- Never commit JWT signing keys, database credentials, seed passwords, raw refresh tokens or production secrets.
- Keep access tokens short lived. Refresh tokens stay in protected HttpOnly cookies and only token hashes may be persisted.
- Endpoint authorization must use reusable permission policies, not scattered role-name comparisons.
- The API must not run migrations at startup; use `Forjix.DatabaseMigrator`.

## Architecture

- `Forjix.Domain` must not reference EF Core, ASP.NET Core, HTTP clients, SQL, or provider SDKs.
- `Forjix.Application` may reference only Domain and owns use cases and abstractions.
- `Forjix.Infrastructure` implements persistence, authentication, tenancy, secrets, and technical services.
- `Forjix.Integrations` contains external adapters/providers. External models must not leak into Domain.
- `Forjix.Api` is the composition and HTTP boundary.
- Avoid circular references, generic repositories, unnecessary libraries, and premature distributed architecture.

## Data and security

- Never commit connection strings, passwords, API keys, access tokens, or refresh tokens.
- Store timestamps in UTC and money as `decimal`.
- Keep business audit logs separate from technical logs.
- Never log passwords, tokens, connection strings, secrets, or payment-card data.
- Negative stock is disabled by default and may only become a validated tenant setting.
- Never update an inventory balance without generating an InventoryMovement in the same transaction.
- A sale never updates inventory directly. Sale creation/cancellation and their inventory movements must commit in one transaction.

## Scope control

- The completed phases include administration, audit, categories, products and inventory movements. Sales, checkout, fiscal features, payment providers, mobile and multi-store behavior remain out of scope until explicitly requested.
- External integrations must use internal contracts plus adapters and provider resolution; never customer-specific conditionals.

## Regression gate

- Every change involving tenancy, authentication, migrations, permissions, or database connections must keep the real SQL Server multi-tenant integration suite passing.
- Do not replace physical tenant-database isolation tests with mocks or an in-memory provider.
