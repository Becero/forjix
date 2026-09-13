# Stack tecnológica

## Backend

| Tecnologia | Versão declarada | Uso |
| --- | --- | --- |
| C# | `LangVersion=latest` | Backend, domínio, migrator e testes. |
| .NET | `net10.0` | Todos os projetos .NET. |
| ASP.NET Core | 10.0 | API, controllers, middleware, DI, health checks, Problem Details e arquivos estáticos. |
| Entity Framework Core | 10.0.4 | Mapeamento, migrations e persistência. |
| EF Core SQL Server | 10.0.4 | Provider exclusivo dos dois tipos de banco. |
| JWT Bearer | 10.0.11 | Autenticação da API. |
| Identity Core | 10.0.11 | `PasswordHasher<User>`. |
| IdentityModel/JWT | 8.19.2 | Emissão e validação de JWT. |
| Serilog.AspNetCore | 10.0.0 | Logs estruturados de requisição. |
| Microsoft.AspNetCore.OpenApi | 10.0.11 | Documento OpenAPI no ambiente Development. |

Configuração global: nullable e implicit usings habilitados, análise `latest-recommended`, style enforcement e warnings tratados como erros.

## Frontend

| Tecnologia | Versão declarada | Uso |
| --- | --- | --- |
| TypeScript | `~5.8.2` | Código Angular. |
| Angular | `^20.0.0` | Componentes standalone, router, forms e DI. |
| Angular CLI/build | `^20.0.4` | Build e servidor de desenvolvimento. |
| RxJS | `~7.8.0` | HTTP, refresh compartilhado e composição assíncrona. |
| Zone.js | `~0.15.0` | Detecção de mudanças. |
| SCSS | configuração Angular | Estilos globais e de componentes. |
| Jasmine/Karma | Jasmine `~5.7`, Karma `~6.4` | Testes unitários do frontend. |

O frontend usa componentes standalone e lazy loading via `loadComponent`; não há NgModules de features.

## Testes e ferramentas

| Ferramenta | Versão | Uso |
| --- | --- | --- |
| xUnit | 2.9.3 | Testes .NET. |
| Microsoft.NET.Test.Sdk | 17.14.1 | Runner. |
| coverlet.collector | 6.0.4 | Coleta de cobertura. |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.11 | `WebApplicationFactory<Program>`. |
| `dotnet-ef` | 10.0.4 | Ferramenta local fixada em `dotnet-tools.json`. |
| Docker Compose | imagem SQL Server 2022 | SQL Server local na porta host 14333 e volume nomeado. |
| Docker | Node 22 Alpine, SDK/runtime .NET 10 | Build multi-stage de produção. |
| Render Blueprint | `render.yaml` | Web Service Docker de homologação. |

## Formatos e protocolos

- REST/JSON sobre HTTP(S), Problem Details para erros e bearer JWT.
- Cookie de refresh HttpOnly; URLs de API relativas para same-origin.
- SQL Server; dinheiro em `decimal(18,2)`, quantidade em `decimal(18,3)`, timestamps em `datetimeoffset`.
- Exportação de relatório em SpreadsheetML com extensão `.xls` e PDF simples gerado pela aplicação.
- Correlation ID no header `X-Correlation-ID`.
