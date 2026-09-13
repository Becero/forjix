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

## Customization

Customer differences must use plan features, tenant feature overrides, settings and adapters/providers. Customer-specific branches and conditionals are prohibited.

## Initial scope

This bootstrap includes identity/access entities, the two DbContexts, tenancy contracts, API infrastructure and the Angular shell. It intentionally excludes commercial modules and external provider implementations.
