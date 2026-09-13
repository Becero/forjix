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

Initialize secrets for the API and set only local connection strings:

```powershell
dotnet user-secrets init --project src/Forjix.Api
dotnet user-secrets set "ConnectionStrings:ForjixMaster" "Server=localhost,14333;Database=ForjixMaster;User Id=sa;Password=<LOCAL_PASSWORD>;TrustServerCertificate=True" --project src/Forjix.Api
```

Do not put the connection string in `appsettings.json`, `.env.example`, documentation, commits or logs.

## Backend

```powershell
dotnet restore
dotnet build --no-restore
dotnet run --project src/Forjix.Api
```

## Frontend

```powershell
cd web/forjix-web
npm install
npm start
```

## Validation

```powershell
dotnet test
npm run build --prefix web/forjix-web
```

Docker validation additionally requires Docker Desktop:

```powershell
docker compose config
```
