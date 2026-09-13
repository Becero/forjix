# Continuous integration

The workflow at `.github/workflows/ci.yml` runs for every pull request and every push to `main` on Ubuntu. Critical steps do not use `continue-on-error`.

## Pipeline

1. Generate and mask job-local SQL, administrator and JWT credentials.
2. Restore and build the .NET solution in Release.
3. Run Domain, Application and HTTP-boundary tests.
4. Start `mcr.microsoft.com/mssql/server:2022-latest` on local port `14333` and wait for a successful SQL query.
5. Run `Forjix.DatabaseMigrator` twice against `ForjixMaster_CI`, `Forjix_EmpresaA_CI` and `Forjix_EmpresaB_CI`.
6. Run the SQL integration category against the real databases.
7. Install Angular dependencies with `npm ci` and build the production bundle.
8. Remove the SQL container even when an earlier step fails.

The two CI tenants are `empresa-a-ci` and `empresa-b-ci`. Both intentionally contain `admin@teste.local`; tenant-specific marker permissions and concurrent authenticated requests prove that identity, permissions, `TenantContext`, resolver and physical `DbContext` do not cross tenants.

## Secrets

No persistent GitHub Actions secret is required. Strong credentials are generated with `openssl` for each job, immediately masked and exported only to that job through `GITHUB_ENV`. They are unrelated to all development and production credentials.

Do not replace these values with committed strings. An organization may move generation to an approved external secret manager later without changing application configuration keys.

## Run locally with Docker

Use a new strong local password and random JWT key in the current terminal. The placeholders below must be replaced locally and must never be committed:

```powershell
$env:DOTNET_ENVIRONMENT = "CI"
$env:ASPNETCORE_ENVIRONMENT = "CI"
$env:FORJIX_RUN_SQL_TESTS = "true"
$env:FORJIX_CI_ADMIN_PASSWORD = "<LOCAL_CI_ADMIN_PASSWORD>"
$env:Jwt__SigningKey = "<RANDOM_LOCAL_KEY_AT_LEAST_32_BYTES>"
$env:ConnectionStrings__ForjixMaster = "Server=localhost,14333;Database=ForjixMaster_CI;User Id=sa;Password=<LOCAL_SQL_PASSWORD>;TrustServerCertificate=True"
$env:TenantDatabases__EmpresaA_CI = "Server=localhost,14333;Database=Forjix_EmpresaA_CI;User Id=sa;Password=<LOCAL_SQL_PASSWORD>;TrustServerCertificate=True"
$env:TenantDatabases__EmpresaB_CI = "Server=localhost,14333;Database=Forjix_EmpresaB_CI;User Id=sa;Password=<LOCAL_SQL_PASSWORD>;TrustServerCertificate=True"
$env:Forjix__IntegrationSeed__Enabled = "true"
$env:Forjix__IntegrationSeed__AdminPassword = $env:FORJIX_CI_ADMIN_PASSWORD

docker run --detach --name forjix-sql-ci --env ACCEPT_EULA=Y --env MSSQL_PID=Developer --env MSSQL_SA_PASSWORD="<LOCAL_SQL_PASSWORD>" --publish 14333:1433 mcr.microsoft.com/mssql/server:2022-latest
dotnet restore Forjix.sln
dotnet build Forjix.sln --configuration Release --no-restore
dotnet run --project tools/Forjix.DatabaseMigrator --configuration Release --no-build
dotnet run --project tools/Forjix.DatabaseMigrator --configuration Release --no-build
dotnet test tests/Forjix.IntegrationTests/Forjix.IntegrationTests.csproj --configuration Release --no-build --filter "Category=SqlIntegration"
npm ci --prefix web/forjix-web
npm run build --prefix web/forjix-web
docker rm --force forjix-sql-ci
```

Stop immediately if any database name or server differs from the isolated CI targets.

## LocalDB, Docker and CI

| Environment | Engine | Purpose |
| --- | --- | --- |
| LocalDB | SQL Server for Windows | Fast developer verification using trusted local authentication |
| Docker local | SQL Server 2022 Linux | Closest local reproduction of the GitHub Actions database |
| GitHub Actions | Fresh official SQL Server 2022 container | Mandatory isolated regression gate with ephemeral credentials |

LocalDB is a valid real-SQL smoke test, but Docker remains the closest reproduction of Linux CI. An in-memory provider is not proof of physical database isolation.
