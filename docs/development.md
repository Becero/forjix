# Development

## Requirements

- .NET SDK 10
- Node.js 22+
- Docker Desktop with Docker Compose

## SQL Server

Copy `.env.example` to `.env`, replace the placeholder with a strong local password and start SQL Server:

```powershell
Copy-Item .env.example .env
docker compose up -d sqlserver
```

The local SQL Server listens on `localhost,14333`. The `.env` file is ignored by Git.

## User Secrets

Configure the API secrets. Use a random JWT key with at least 32 bytes:

```powershell
dotnet user-secrets init --project src/Forjix.Api
dotnet user-secrets set "ConnectionStrings:ForjixMaster" "Server=localhost,14333;Database=ForjixMaster;User Id=sa;Password=<LOCAL_PASSWORD>;TrustServerCertificate=True" --project src/Forjix.Api
dotnet user-secrets set "Jwt:SigningKey" "<RANDOM_LOCAL_KEY_AT_LEAST_32_BYTES>" --project src/Forjix.Api
```

Configure the migrator independently. The demo password must have at least 12 characters and exists only in local Development configuration:

```powershell
dotnet user-secrets set "ConnectionStrings:ForjixMaster" "Server=localhost,14333;Database=ForjixMaster;User Id=sa;Password=<LOCAL_PASSWORD>;TrustServerCertificate=True" --project tools/Forjix.DatabaseMigrator
dotnet user-secrets set "TenantDatabases:empresa-demo" "Server=localhost,14333;Database=Forjix_EmpresaDemo;User Id=sa;Password=<LOCAL_PASSWORD>;TrustServerCertificate=True" --project tools/Forjix.DatabaseMigrator
dotnet user-secrets set "Forjix:DevelopmentSeed:Enabled" "true" --project tools/Forjix.DatabaseMigrator
dotnet user-secrets set "Forjix:DevelopmentSeed:AdminPassword" "<LOCAL_DEMO_PASSWORD>" --project tools/Forjix.DatabaseMigrator
```

Expose the tenant connection to the API using the same secret-reference key:

```powershell
dotnet user-secrets set "TenantDatabases:empresa-demo" "Server=localhost,14333;Database=Forjix_EmpresaDemo;User Id=sa;Password=<LOCAL_PASSWORD>;TrustServerCertificate=True" --project src/Forjix.Api
```

Do not put connection strings, JWT keys or passwords in `appsettings.json`, `.env.example`, documentation, commits or logs.

## Provision Empresa Demo

With `DOTNET_ENVIRONMENT=Development`, the controlled migrator creates or updates `ForjixMaster`, provisions `Forjix_EmpresaDemo`, applies both migration sets and idempotently creates the administrator:

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project tools/Forjix.DatabaseMigrator
```

Development login: tenant `empresa-demo`, e-mail `admin@demo.com`, and the password stored in User Secrets. Run the migrator a second time to verify that no tenant, role, permission or user is duplicated.
For Development only, each seed execution resets this administrator to the locally configured password.

## Backend

```powershell
dotnet restore
dotnet build --no-restore
dotnet run --project src/Forjix.Api
```

The Visual Studio `http` and `https` profiles both define `Development`. Debug builds also load the API User Secrets explicitly, so pressing F5 remains independent from the IDE's selected launch profile. Release builds never use this fallback and must receive configuration from the deployment environment or secret manager.

## Frontend

```powershell
cd web/forjix-web
npm ci
npm start
```

The Angular development server proxies `/api` to the HTTPS API address configured in `proxy.conf.json`.

## Authentication smoke test

1. Open `http://localhost:4200`.
2. Sign in with `empresa-demo` and `admin@demo.com`.
3. Confirm that `/app` shows Empresa Demo and the menus allowed by the administrator permissions.
4. Open Users, Access groups, Audit, Categories and Products and verify the seeded catalog.
5. Reload the page; the HttpOnly refresh cookie must restore the in-memory access token.
6. Sign out and confirm that the refresh token no longer restores the session.

## Validation

```powershell
dotnet restore
dotnet build --no-restore
dotnet test
npm ci --prefix web/forjix-web
npm run build --prefix web/forjix-web
```

Docker validation additionally requires Docker Desktop:

```powershell
docker compose config
```

Without `FORJIX_RUN_SQL_TESTS=true`, the external SQL acceptance tests remain explicitly skipped while unit and HTTP-boundary tests run normally. They are mandatory in CI and must not be unconditionally enabled against an unknown database.

For the complete CI-equivalent flow, including isolated environment variables and two migrator executions, follow [ci.md](ci.md). On Windows, LocalDB is convenient for local verification. Docker matches the Linux CI engine more closely. Neither may point to development or production databases.
