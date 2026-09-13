# Forjix

Commercial management platform for small and medium businesses. This repository is independent from the institutional website and the former demonstration application.

The current delivery implements the multi-tenant identity foundation and the first operational modules: real login, JWT access tokens, rotating refresh tokens, permission policies, user and access-group management, audit trails, categories and products. It includes controlled provisioning and a real SQL Server 2022 regression gate. Inventory, sales, checkout, reports and external integrations remain reserved for later phases.

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

The API exposes health endpoints at `/api/health` and `/api/health/live`. Authentication is available at `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout`, and the authenticated context at `/api/me`. Authenticated management endpoints are grouped under `/api/users`, `/api/roles`, `/api/audit`, `/api/categories` and `/api/products`. In Development, OpenAPI is available at `/openapi/v1.json`.

## Continuous integration

Pull requests and pushes to `main` run [.github/workflows/ci.yml](.github/workflows/ci.yml). The workflow builds the .NET solution, starts the official SQL Server 2022 container, runs the controlled migrator twice, executes the real multi-tenant authentication suite and produces the Angular production bundle. CI credentials are generated for each job and are never committed or shared with development or production.

See [docs/ci.md](docs/ci.md) for database names, security choices and local reproduction instructions.
