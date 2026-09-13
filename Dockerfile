# syntax=docker/dockerfile:1

FROM node:22-alpine AS web-build
WORKDIR /src/web/forjix-web
COPY web/forjix-web/package.json web/forjix-web/package-lock.json ./
RUN npm ci
COPY web/forjix-web/ ./
RUN npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY Directory.Build.props Forjix.sln ./
COPY src/Forjix.Domain/Forjix.Domain.csproj src/Forjix.Domain/
COPY src/Forjix.Application/Forjix.Application.csproj src/Forjix.Application/
COPY src/Forjix.Infrastructure/Forjix.Infrastructure.csproj src/Forjix.Infrastructure/
COPY src/Forjix.Integrations/Forjix.Integrations.csproj src/Forjix.Integrations/
COPY src/Forjix.Api/Forjix.Api.csproj src/Forjix.Api/
RUN dotnet restore src/Forjix.Api/Forjix.Api.csproj
COPY src/ src/
RUN dotnet publish src/Forjix.Api/Forjix.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=10000
COPY --from=api-build --chown=app:app /app/publish ./
COPY --from=web-build --chown=app:app /src/web/forjix-web/dist/forjix-web/browser ./wwwroot
RUN mkdir -p /app/App_Data && chown -R app:app /app/App_Data
USER app
EXPOSE 10000
ENTRYPOINT ["dotnet", "Forjix.Api.dll"]
