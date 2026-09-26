# Banco de dados

## Estratégia

O provider é exclusivamente `Microsoft.EntityFrameworkCore.SqlServer`. Há dois modelos físicos:

- **ForjixMaster:** catálogo central de tenants, planos, assinaturas, localização lógica dos bancos, settings/features e histórico do migrator.
- **Tenant Database:** um banco operacional independente por empresa, contendo identidade, catálogo, operação e auditoria. As tabelas comerciais não possuem `TenantId` porque o isolamento é físico.

## DbContexts

`ForjixMasterDbContext` é registrado scoped com `ConnectionStrings:ForjixMaster`. `TenantDbContext` não é registrado com uma conexão fixa: `TenantDbContextFactory.Create(ResolvedTenantDatabase)` instancia um contexto para a operação usando a connection string resolvida.

| Contexto | DbSets/tabelas da aplicação |
| --- | --- |
| Master | `Tenants`, `Plans`, `Subscriptions`, `TenantDatabases`, `TenantSettings`, `TenantFeatures`, `MigrationExecutions` |
| Tenant | `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`, `RefreshTokens`, `AuditLogs`, `Categories`, `Products`, `Inventories`, `InventoryMovements`, `Sales`, `SaleItems`, `SaleSequences`, `Customers`, `Suppliers`, `Purchases`, `PurchaseItems`, `PurchaseSequences`, `CashRegisters`, `CashSessions`, `CashMovements`, `FinancialCategories`, `AccountsReceivable`, `AccountsPayable`, `FinancialPayments` |

## Configuração do modelo

O projeto usa classes agregadoras estáticas `MasterModelConfiguration`, `TenantModelConfiguration` e `FinancialModelConfiguration`, chamadas por `OnModelCreating`; não há classes individuais `IEntityTypeConfiguration<T>`. Elas definem nomes, tamanhos, precisão, índices, FKs, delete behaviors e rowversions.

Padrões:

- PKs de agregados em `uniqueidentifier`; logs/movimentos usam `bigint IDENTITY`; sequências diárias usam `date`.
- Dinheiro: `decimal(18,2)`; quantidades: `decimal(18,3)`.
- Concorrência: `rowversion` em `Products`, `Inventories`, `Sales`, `Purchases`, `CashSessions`, `RefreshTokens`, `AccountsReceivable`, `AccountsPayable` e `FinancialPayments`.
- Exclusão `Cascade`: tabelas de associação, refresh tokens, itens em relação ao cabeçalho, e metadados dependentes de tenant no Master.
- Exclusão `Restrict`: vínculos comerciais que não devem desaparecer por cascata.
- Índices filtrados únicos em documentos/barcodes opcionais.

## Transações

- Venda: isolamento `Serializable`, idempotency key, validação de caixa aberto, criação de venda/itens, movimentos de estoque, movimento de caixa em dinheiro e auditoria no mesmo commit.
- Cancelamento de venda: cabeçalho, reposição de estoque, movimentos e auditoria em uma transação.
- Compra: criação usa `Serializable`; recebimento altera status e estoque com movimentos no mesmo commit.
- Financeiro: títulos, baixas, estornos, movimentos em dinheiro e auditoria usam uma transação; vendas Deferred e recebimentos de compras criam títulos dentro da transação de origem.
- Estoque manual: saldo, movimento e auditoria no mesmo commit.
- Caixa: abertura `Serializable`; demais operações transacionais e protegidas por rowversion.

## Migrations

Master possui 3 migrations; tenant possui 9. Financeiro V1 adiciona quatro tabelas, constraints e permissões. Consulte o [modelo financeiro](12-financeiro.md) para os detalhes atuais. Os snapshots atuais foram usados como fonte do dicionário em [06-dicionario-de-dados.md](06-dicionario-de-dados.md). A API não chama `Migrate`; somente o `Forjix.DatabaseMigrator` aplica schema.

Cada banco também recebe a tabela técnica `__EFMigrationsHistory` administrada pelo EF Core. Ela não aparece no model snapshot como entidade da aplicação; por isso seu DDL interno não é tratado como tabela de negócio no dicionário.

## Orçamentos V1 — atualização de 26/09/2026

O módulo comercial de orçamentos está implementado. A migration tenant `20260926144839_AddQuotesV1` acrescenta `Quotes`, `QuoteItems` e `QuoteSequences`, sete permissões `quotes.*` e os endpoints `/api/quotes`. Criação não movimenta estoque nem financeiro; conversão aprovada reutiliza a venda em transação única. Consulte [Orçamentos V1](13-orcamentos.md) para esquema, contratos, status, testes e limitações.

## Estoque Avançado V1 — atualização de 26/09/2026

Inventário físico implementado como `Stocktake`, preservando `Inventories` como saldo único. Migration `20260926150829_AddAdvancedInventoryV1` adiciona `Stocktakes`, `StocktakeItems`, `StocktakeSequences`, campos opcionais de motivo/observação em movimentos e sete permissões. Finalização incremental e transacional, com bloqueio de contagens desatualizadas. Alertas, histórico integrado, ajustes e relatórios disponíveis. Consulte [Estoque Avançado / Inventário V1](14-estoque-avancado.md) para contratos, regras, testes e limitações.
