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

## Scope control

- Phase 1 bootstrap must not implement Products, Inventory, Sales, Dashboard, fiscal features, payment providers, mobile, or multi-store behavior.
- External integrations must use internal contracts plus adapters and provider resolution; never customer-specific conditionals.
