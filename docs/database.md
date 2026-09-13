# Databases and migrations

## Databases

- `ForjixMasterDbContext`: central platform administration.
- `TenantDbContext`: identity, permissions, refresh tokens, audit data, categories, products, inventory and stock movements for one tenant.

No password is stored in `TenantDatabase`. `SecretReference` points to a secret-provider entry.

The tenant setting `AllowNegativeStock` belongs to the central tenant configuration and defaults to `false`. The stock rule itself is outside this bootstrap.

## Create migrations

Restore the repository tool manifest before using EF commands:

```powershell
dotnet tool restore
```

Master migration:

```powershell
dotnet ef migrations add <Name> --context ForjixMasterDbContext --project src/Forjix.Infrastructure --startup-project src/Forjix.Infrastructure --output-dir Persistence/Master/Migrations
```

Tenant template migration:

```powershell
dotnet ef migrations add <Name> --context TenantDbContext --project src/Forjix.Infrastructure --startup-project src/Forjix.Infrastructure --output-dir Persistence/Tenant/Migrations
```

## Apply migrations and provision Development

The API never applies migrations at startup. `Forjix.DatabaseMigrator` first migrates `ForjixMaster`, optionally provisions Empresa Demo in Development, then discovers active tenants and migrates each isolated database. Every tenant attempt is recorded in `MigrationExecutions`; `TenantDatabase.SchemaVersion` and `LastMigratedAt` are updated after success.

Tenant connection strings are loaded through each `TenantDatabase.SecretReference`. The demo reference is `TenantDatabases:empresa-demo`; only the reference is persisted centrally.

The development seed is idempotent by construction: it looks up the plan, tenant, subscription, setting, permissions, role, grants, user and user-role relation by stable keys before inserting. Its real SQL Server acceptance test must execute the migrator twice and inspect both databases.

The current tenant schema uses SQL Server `rowversion` on refresh tokens, products and inventory. It prevents concurrent refresh rotation, rejects stale product updates and ensures simultaneous stock movements cannot silently overwrite a balance.

## CI databases

CI uses only these disposable names:

- `ForjixMaster_CI`
- `Forjix_EmpresaA_CI`
- `Forjix_EmpresaB_CI`

The CI-only seed is enabled only when `DOTNET_ENVIRONMENT=CI` and `Forjix:IntegrationSeed:Enabled=true`. It provisions `empresa-a-ci` and `empresa-b-ci`, stores only their secret-reference keys in Master, migrates each physical tenant database and seeds equivalent users. The migrator is executed twice before acceptance tests; assertions over Master and both tenant databases prove migrations and idempotence.

User Secrets are loaded by the migrator only in Development. This prevents a developer's local secrets from overriding explicitly isolated CI connection strings.
