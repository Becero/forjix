# Databases and migrations

## Databases

- `ForjixMasterDbContext`: central platform administration.
- `TenantDbContext`: identity, permissions, refresh tokens and audit data for one tenant.

No password is stored in `TenantDatabase`. `SecretReference` points to a secret-provider entry.

The tenant setting `AllowNegativeStock` belongs to the central tenant configuration and defaults to `false`. The stock rule itself is outside this bootstrap.

## Create migrations

Restore the repository tool manifest before using EF commands:

```powershell
dotnet tool restore
```

Master migration:

```powershell
dotnet ef migrations add <Name> --context ForjixMasterDbContext --project src/Forjix.Infrastructure --startup-project src/Forjix.Api --output-dir Persistence/Master/Migrations
```

Tenant template migration:

```powershell
dotnet ef migrations add <Name> --context TenantDbContext --project src/Forjix.Infrastructure --startup-project src/Forjix.Api --output-dir Persistence/Tenant/Migrations
```

## Apply migrations and provision Development

The API never applies migrations at startup. `Forjix.DatabaseMigrator` first migrates `ForjixMaster`, optionally provisions Empresa Demo in Development, then discovers active tenants and migrates each isolated database. Every tenant attempt is recorded in `MigrationExecutions`; `TenantDatabase.SchemaVersion` and `LastMigratedAt` are updated after success.

Tenant connection strings are loaded through each `TenantDatabase.SecretReference`. The demo reference is `TenantDatabases:empresa-demo`; only the reference is persisted centrally.

The development seed is idempotent by construction: it looks up the plan, tenant, subscription, setting, permissions, role, grants, user and user-role relation by stable keys before inserting. Its real SQL Server acceptance test must execute the migrator twice and inspect both databases.

The current tenant schema uses a SQL Server `rowversion` on refresh tokens so concurrent rotation attempts cannot both succeed.
