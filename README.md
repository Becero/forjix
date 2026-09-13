# Forjix

Commercial management platform for small and medium businesses. This repository is independent from the institutional website and the former demonstration application.

The current delivery is the first complete commercial V1: multi-tenant identity, users and access groups, audit, catalog, inventory, customers, suppliers, purchases, sales, point of sale, cash register, dashboard, reports and tenant settings. It includes controlled provisioning, an idempotent Development demo and a real SQL Server regression gate. Fiscal, payment-provider, multi-store and mobile integrations remain reserved for later phases.

## Stack

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core 10 / SQL Server
- Angular 20 with standalone components
- SQL Server 2022 container for local development

## Repository

```text
src/       Domain, Application, Infrastructure, Integrations and API
tools/     Controlled database migrator
tests/     Domain, Application and Integration tests
web/       Angular application
docs/      Architecture, development and database documentation
```

## Quick start

Read [development.md](docs/development.md) before running the solution. It explains Docker, User Secrets, migrations and the two applications.

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build

cd web/forjix-web
npm ci
npm run build
```

The API exposes health endpoints at `/api/health` and `/api/health/live`. Authentication is available at `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout`, and the authenticated context at `/api/me`. Operational endpoints are grouped under `/api/products`, `/api/inventory`, `/api/customers`, `/api/suppliers`, `/api/purchases`, `/api/sales`, `/api/cash`, `/api/dashboard`, `/api/reports` and `/api/settings`. In Development, OpenAPI is available at `/openapi/v1.json`.

## Continuous integration

Pull requests and pushes to `main` run [.github/workflows/ci.yml](.github/workflows/ci.yml). The workflow builds the .NET solution, starts the official SQL Server 2022 container, runs the controlled migrator twice, executes the real multi-tenant authentication suite and produces the Angular production bundle. CI credentials are generated for each job and are never committed or shared with development or production.

See [docs/ci.md](docs/ci.md) for database names, security choices and local reproduction instructions.
