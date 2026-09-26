# Documentação técnica da Forjix

Documentação gerada por inspeção do código existente. Os documentos anteriores já presentes em `docs/` foram mantidos como histórico e referência operacional; este índice destaca o conjunto solicitado.

## Índice

1. [Visão geral](00-visao-geral.md)
2. [Stack tecnológica](01-stack-tecnologica.md)
3. [Arquitetura](02-arquitetura.md)
4. [Backend](03-backend.md)
5. [Frontend Angular](04-frontend.md)
6. [Banco de dados](05-banco-de-dados.md)
7. [Dicionário de dados](06-dicionario-de-dados.md)
8. [Multitenancy](07-multitenancy.md)
9. [Autenticação e autorização](08-autenticacao-autorizacao.md)
10. [Módulos do sistema](09-modulos-do-sistema.md)
11. [API e endpoints](10-api-endpoints.md)
12. [Migrations e seed](11-migrations-e-seed.md)
13. [Financeiro V1: modelo, API, integrações e operação](12-financeiro.md)
14. [Orçamentos V1](13-orcamentos.md)
15. [Estoque Avançado / Inventário V1](14-estoque-avancado.md)
16. [Segurança](15-seguranca.md)
17. [Roadmap técnico](18-roadmap-tecnico.md)

## Escopo analisado

Foram inspecionados:

- `Forjix.sln`, `Directory.Build.props`, `dotnet-tools.json`, arquivos `.csproj`, `package.json`, `angular.json` e `tsconfig*`;
- todos os arquivos C# em `src/Forjix.Domain`, `src/Forjix.Application`, `src/Forjix.Infrastructure`, `src/Forjix.Integrations` e `src/Forjix.Api`;
- controllers, middleware, autenticação, autorização, DI, stores, serviços, entidades, enums e configurações EF;
- ambos os `DbContext`, todas as migrations, designers e model snapshots do Master e do tenant;
- `tools/Forjix.DatabaseMigrator`, seus fluxos de provisionamento e seeds;
- todos os arquivos TypeScript/HTML/SCSS/configuração em `web/forjix-web/src` e os serviços, guards, interceptor, store e rotas Angular;
- todos os arquivos dos três projetos em `tests/`, incluindo infraestrutura de testes SQL;
- scripts PowerShell, Dockerfiles, `docker-compose.yml`, `render.yaml`, GitHub Actions e documentação preexistente;
- buscas globais por controllers, rotas, entidades, modelos, tabelas, migrations, permissões, `TODO` e `FIXME`.

Arquivos gerados, artefatos de build (`bin`, `obj`, `node_modules`, `dist`), conteúdo Git interno e valores de User Secrets/variáveis de ambiente não foram tratados como fonte funcional.

## Limites e incertezas

- A documentação descreve o modelo EF e migrations versionadas; não foi feita introspecção em um banco implantado. Drift manual fora do repositório não pode ser determinado.
- Tipos físicos e constraints são os do provider SQL Server conforme migrations/snapshot. A tabela convencional `__EFMigrationsHistory` não tem definição própria no projeto.
- Não é possível confirmar, apenas pelo repositório, quais variáveis, segredos, origins CORS, discos ou chaves de Data Protection estão configurados em ambientes externos.
- Contagens de testes são casos descobertos no código/runner e podem variar com teorias ou alterações futuras; testes SQL dependem de opt-in e infraestrutura externa.
- “Implementado” indica código existente, não certificação de desempenho, segurança, conformidade fiscal ou prontidão comercial.
- Os nomes de classes, modelos, controllers, permissões, tabelas e migrations citados foram cruzados com o código. Onde não havia evidência suficiente, o texto registra explicitamente a limitação em vez de inferir comportamento.

## Orçamentos V1 — atualização de 26/09/2026

O módulo comercial de orçamentos está implementado. A migration tenant `20260926144839_AddQuotesV1` acrescenta `Quotes`, `QuoteItems` e `QuoteSequences`, sete permissões `quotes.*` e os endpoints `/api/quotes`. Criação não movimenta estoque nem financeiro; conversão aprovada reutiliza a venda em transação única. Consulte [Orçamentos V1](13-orcamentos.md) para esquema, contratos, status, testes e limitações.

## Estoque Avançado V1 — atualização de 26/09/2026

Inventário físico implementado como `Stocktake`, preservando `Inventories` como saldo único. Migration `20260926150829_AddAdvancedInventoryV1` adiciona `Stocktakes`, `StocktakeItems`, `StocktakeSequences`, campos opcionais de motivo/observação em movimentos e sete permissões. Finalização incremental e transacional, com bloqueio de contagens desatualizadas. Alertas, histórico integrado, ajustes e relatórios disponíveis. Consulte [Estoque Avançado / Inventário V1](14-estoque-avancado.md) para contratos, regras, testes e limitações.
