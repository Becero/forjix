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

## Apply migrations

During Development, a single database may be updated explicitly with `dotnet ef database update`. Production must use `Forjix.DatabaseMigrator`; the API must not silently migrate all tenant databases during startup.

The migrator is only a bootstrap placeholder in this delivery. Fleet discovery, secret resolution, execution records and failure policies are implemented in a later approved step.
