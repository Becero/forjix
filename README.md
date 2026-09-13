# Forjix

Commercial management platform for small and medium businesses. This repository is independent from the institutional website and the former demonstration application.

The current delivery is the architectural bootstrap only. Product, inventory, sales and dashboard modules are intentionally not implemented yet.

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
npm install
npm run build
```

The API exposes basic health endpoints at `/api/health` and `/api/health/live`. In Development, the OpenAPI document is available at `/openapi/v1.json`.
