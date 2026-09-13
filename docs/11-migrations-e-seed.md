# Migrations, provisionamento e seeds

## Responsabilidade do migrador

`tools/Forjix.DatabaseMigrator` é o único executável responsável por aplicar migrations. A API registra os `DbContext`, mas não chama `Database.Migrate`; essa separação evita que várias instâncias da aplicação concorram pela alteração do schema durante o startup.

Fluxo executado por `Program.cs`:

1. carrega configuração e, somente em `Development`, User Secrets;
2. exige `ConnectionStrings:ForjixMaster` e aplica migrations do Master;
3. executa o provisionamento opcional compatível com o ambiente;
4. consulta todos os tenants ativos com banco configurado;
5. resolve cada `TenantDatabase.SecretReference` na configuração;
6. aplica as migrations do tenant em um `ForjixTenantDbContext` novo;
7. atualiza `TenantDatabases.SchemaVersion` e `LastMigratedAt`;
8. registra uma `MigrationExecution` como `Succeeded` ou `Failed`;
9. define código de saída `1` se algum tenant falhar.

## Migrations do ForjixMaster

| Ordem | Migration |
|---:|---|
| 1 | `20260913125248_InitialMaster` |
| 2 | `20260913130925_AddAuthenticationRuntime` |
| 3 | `20260913132543_ExpandTenantSchemaVersion` |

## Migrations dos tenants

| Ordem | Migration |
|---:|---|
| 1 | `20260913125310_InitialTenantIdentity` |
| 2 | `20260913130929_AddRefreshTokenConcurrency` |
| 3 | `20260913144136_AddAdministrationAndCatalog` |
| 4 | `20260913154233_AddInventoryAndStockMovements` |
| 5 | `20260913163543_AddSalesAndPointOfSale` |
| 6 | `20260913164107_AddCustomers` |
| 7 | `20260913164531_AddSuppliersAndPurchases` |
| 8 | `20260913164852_AddCashRegister` |

Além das tabelas da aplicação, o EF Core mantém `__EFMigrationsHistory` em cada banco migrado. A definição dessa tabela é convenção do provider e não aparece como entidade no modelo Forjix.

## DevelopmentSeed

Só é considerado quando o ambiente é exatamente `Development` e `Forjix:DevelopmentSeed:Enabled=true`. Exige:

- `Forjix:DevelopmentSeed:AdminPassword`, com pelo menos 12 caracteres;
- `TenantDatabases:empresa-demo`, contendo a conexão do banco demonstrativo;
- `ConnectionStrings:ForjixMaster`.

Provisiona o tenant `empresa-demo`, identidade administrativa, papéis/permissões e dados comerciais demonstrativos. O código de demonstração inclui categorias, produtos, inventário inicial, caixa e vendas.

## IntegrationSeed

Só executa em ambiente `CI` com `Forjix:IntegrationSeed:Enabled=true`. Exige `Forjix:IntegrationSeed:AdminPassword`, `TenantDatabases:EmpresaA_CI` e `TenantDatabases:EmpresaB_CI`. Provisiona dois tenants isolados, marcadores de permissão distintos e um usuário sem privilégios para os testes de integração. Não cria o conjunto comercial de demonstração.

Os testes SQL ainda são condicionados a `FORJIX_RUN_SQL_TESTS=true` e a uma infraestrutura de banco previamente disponível.

## HomologationProvisioning

Só executa em `Production` quando `Forjix:HomologationProvisioning:Enabled=true`. Há uma trava adicional: `Forjix:HomologationProvisioning:Confirmation` deve ser exatamente `PROVISION_FORJIX_HOMOLOGATION`.

Chaves obrigatórias externas:

- `TenantName`, `TenantSlug`, `DatabaseName`;
- `TenantSecretReference`, que aponta para outra chave de configuração que contém a conexão;
- `AdminEmail`, `AdminName`, `AdminPassword` (mínimo de 12 caracteres).

`SeedCommercialDemo` é opcional e controla os dados comerciais. Nenhum valor sensível deve ser gravado nos arquivos versionados.

## Idempotência observada

`ProvisionTenantAsync` procura o tenant por slug, cria registros ausentes e sincroniza os vínculos necessários. Seeds comerciais verificam a existência de seus registros e as migrations do EF são naturalmente incrementais. Assim, reexecuções não recriam indiscriminadamente tenants, papéis, permissões e catálogo.

Há efeitos deliberados em reexecução: os dados do administrador são normalizados, sua senha e `SecurityStamp` são atualizados; a base comercial só é acrescentada sob as condições codificadas. Portanto, “idempotente” aqui significa estado final convergente, não ausência total de escrita.

## Exemplo sanitizado

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
$env:ConnectionStrings__ForjixMaster = "<CONEXAO_MASTER>"
Set-Item -LiteralPath 'Env:TenantDatabases__empresa-demo' -Value '<CONEXAO_TENANT>'
$env:Forjix__DevelopmentSeed__Enabled = "true"
$env:Forjix__DevelopmentSeed__AdminPassword = "<SEGREDO_COM_12_OU_MAIS_CARACTERES>"
dotnet run --project .\tools\Forjix.DatabaseMigrator -c Release
```

O provider de configuração converte `__` em `:`. `Set-Item` evita a ambiguidade do hífen no nome da variável PowerShell. Outras plataformas podem impor regras diferentes, portanto a configuração do ambiente de destino deve ser conferida. O exemplo acima é apenas ilustrativo e não contém credenciais reais.
