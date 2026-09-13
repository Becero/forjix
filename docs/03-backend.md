# Backend

## Entidades de domínio

As propriedades abaixo são as propriedades persistentes declaradas nas classes; navegações são descritas separadamente. Tipos anuláveis estão marcados com `?`.

### Master

| Entidade | Responsabilidade | Propriedades C# | Relações e regras observadas |
| --- | --- | --- | --- |
| `Tenant` | Empresa lógica da plataforma. | `Id:Guid`, `Name:string`, `Slug:string`, `Cnpj:string?`, `Status:TenantStatus`, `PlanId:Guid?`, `CreatedAt/UpdatedAt:DateTimeOffset` | Plano opcional; 1:1 database; coleções de assinaturas, settings, features e execuções. Resolver só aceita status `Active`. |
| `Plan` | Plano associado ao tenant/assinatura. | `Id:Guid`, `Code:string`, `Name:string`, `IsActive:bool`, `UserLimit:int`, timestamps | 1:N tenants e subscriptions. O código atual não aplica `UserLimit` nos casos de uso. |
| `Subscription` | Vigência de plano. | `Id`, `TenantId`, `PlanId:Guid`, `Status:SubscriptionStatus`, `StartsAt`, `EndsAt?`, timestamps | Resolver exige assinatura `Active`, iniciada e não expirada. |
| `TenantDatabase` | Metadados do banco operacional. | `Id`, `TenantId:Guid`, `DatabaseName`, `ServerReference`, `SecretReference`, `SchemaVersion:string`, timestamps, `LastMigratedAt?` | 1:1 com tenant; guarda referência, não a connection string. |
| `TenantSetting` | Configuração chave/valor do tenant. | `Id`, `TenantId:Guid`, `Key`, `Value:string`, `UpdatedAt` | Chave única por tenant. Chaves conhecidas ficam em `TenantSettingKeys`. |
| `TenantFeature` | Feature flag com expiração opcional. | `Id`, `TenantId:Guid`, `FeatureCode:string`, `IsEnabled:bool`, `ExpiresAt?`, `UpdatedAt` | Resolver retorna apenas habilitadas e não expiradas. |
| `MigrationExecution` | Histórico operacional do migrator por tenant. | `Id:Guid`, `TenantId:Guid?`, `DatabaseName`, `MigrationType`, `Status`, `AppliedMigration?`, `ErrorSummary?`, `StartedAt/CompletedAt` | Tenant opcional; migrator grava sucesso/falha de cada banco tenant. |

### Identidade e auditoria do tenant

| Entidade | Responsabilidade | Propriedades C# | Relações e regras observadas |
| --- | --- | --- | --- |
| `User` | Identidade local ao banco tenant. | `Id`, `Name`, `Email`, `NormalizedEmail`, `PasswordHash:string`, `IsActive`, `LockedUntil?`, `FailedAccessAttempts:int`, `SecurityStamp:Guid`, timestamps | N:N roles; 1:N refresh tokens. Login rejeita inativo/bloqueado. |
| `Role` | Grupo de acesso. | `Id`, `Name`, `NormalizedName`, `Description?`, `IsSystem`, timestamps | N:N users e permissions. Grupo de sistema pode ser atualizado no serviço, mas seu nome não pode ser alterado. |
| `Permission` | Ação autorizável. | `Id`, `Code`, `Name`, `Description?`, `Module:string` | Catálogo de 29 códigos em `Permissions.Catalog`. |
| `UserRole` | Associação usuário–grupo. | `UserId`, `RoleId:Guid`, `AssignedAt` | PK composta; cascata nas duas FKs. |
| `RolePermission` | Associação grupo–permissão. | `RoleId`, `PermissionId:Guid`, `GrantedAt` | PK composta; cascata nas duas FKs. |
| `RefreshToken` | Sessão renovável persistida por hash. | `Id`, `UserId`, `FamilyId:Guid`, `TokenHash:string`, `ExpiresAt`, `CreatedAt`, `RevokedAt?`, `ReplacedByTokenId:Guid?`, `RowVersion:byte[]` | Pertence a user; hash único; rotação/replay opera por família. |
| `AuditLog` | Auditoria de negócio. | `Id:long`, `UserId:Guid?`, `Action:AuditAction`, `EntityName`, `EntityId?`, `BeforeData?`, `AfterData?`, `IpAddress?`, `CorrelationId`, `OccurredAt` | Não há FK configurada para `UserId`; payloads são JSON quando preenchidos. |

### Catálogo, clientes e estoque

| Entidade | Responsabilidade | Propriedades C# | Relações e regras observadas |
| --- | --- | --- | --- |
| `Category` | Classificação de produtos. | `Id`, `Name`, `Description?`, `IsActive`, timestamps | 1:N products; nome único. Categoria com produto ativo não pode ser desativada pelo serviço. |
| `Product` | Item vendável/comprável. | `Id`, `CategoryId`, `Name`, `Sku`, `Barcode?`, `SalePrice`, `CostPrice`, `MinimumStock:decimal`, `IsActive`, timestamps, `RowVersion` | Categoria N:1; inventário 1:1; itens/movimentos. SKU único; barcode único quando presente. Criar produto cria inventário zerado. |
| `Inventory` | Saldo atual de um produto. | `Id`, `ProductId`, `Quantity:decimal` (private set), `UpdatedAt` (private set), `RowVersion` | `Create` inicia zero. `ApplyMovement` exige quantidade positiva, determina sinal pelo enum e impede saldo negativo quando configuração=false. |
| `InventoryMovement` | Ledger imutável de alteração de saldo. | `Id:long`, `InventoryId`, `ProductId`, `Type`, `Quantity`, `PreviousQuantity`, `NewQuantity:decimal`, `Reason?`, `ReferenceType?`, `ReferenceId?`, `UserId`, `CreatedAt` | FKs para inventory/product/user. Venda, cancelamento e recebimento também geram movimentos. |
| `Customer` | Cliente opcional de venda. | `Id`, `Name`, `Document?`, `Email?`, `Phone?`, `Notes?`, `IsActive`, timestamps | 1:N vendas; documento único quando presente. |

### Vendas, compras e caixa

| Entidade | Responsabilidade | Propriedades C# | Relações e regras observadas |
| --- | --- | --- | --- |
| `Sale` | Cabeçalho da venda. | `Id`, `Number`, `IdempotencyKey`, `Status`, `Subtotal`, `Discount`, `Total`, `PaymentMethod`, `UserId`, `CustomerId?`, `CreatedAt`, `CancelledAt?`, `CancelledByUserId?`, `CancellationReason?`, `RowVersion` | User obrigatório, customer opcional, itens 1:N. `Cancel` só aceita venda ainda não cancelada. |
| `SaleItem` | Snapshot de produto/preço/custo na venda. | `Id`, `SaleId`, `ProductId`, `ProductName`, `Sku`, `Quantity`, `UnitPrice`, `UnitCost`, `Discount`, `Total` | Sale cascata; product restrito. |
| `SaleSequence` | Sequência diária de número `VD-yyyyMMdd-00000`. | `Date:DateOnly`, `LastValue:int` | `Date` é a PK. |
| `Supplier` | Fornecedor. | `Id`, `Name`, `Document?`, `Email?`, `Phone?`, `ContactName?`, `Notes?`, `IsActive`, timestamps | 1:N compras; documento único quando presente. |
| `Purchase` | Cabeçalho de compra. | `Id`, `Number`, `SupplierId`, `Status`, `Total`, `Notes?`, `UserId`, `CreatedAt`, `ReceivedAt?`, `CancelledAt?`, `RowVersion` | Só `Pending` pode receber/cancelar. Recebimento cria movimentos de estoque. |
| `PurchaseItem` | Snapshot de produto/custo na compra. | `Id`, `PurchaseId`, `ProductId`, `ProductName`, `Sku`, `Quantity`, `UnitCost`, `Total` | Purchase cascata; product restrito. |
| `PurchaseSequence` | Sequência diária `CP-yyyyMMdd-00000`. | `Date:DateOnly`, `LastValue:int` | `Date` é a PK. |
| `CashRegister` | Cadastro de caixa. | `Id:Guid`, `Name:string`, `IsActive:bool` | 1:N sessões; o store cria “Caixa principal” se não houver ativo. |
| `CashSession` | Abertura/fechamento. | `Id`, `CashRegisterId`, `OpenedByUserId`, `OpenedAt`, `OpeningAmount`, `ClosedByUserId?`, `ClosedAt?`, `ClosingAmount?`, `Status`, `RowVersion` | Só fecha se aberta; store impede mais de uma sessão aberta por transação serializable. |
| `CashMovement` | Lançamento financeiro da sessão. | `Id:long`, `CashSessionId`, `Type`, `Amount`, `Reason?`, `UserId`, `CreatedAt`, `SaleId?` | FK apenas para sessão. Venda em dinheiro lança `Sale`; fechamento divergente lança `ClosingAdjustment`. |

## Enums

- `TenantStatus`: `Provisioning`, `Active`, `Suspended`, `Inactive`.
- `SubscriptionStatus`: `Pending`, `Active`, `PastDue`, `Suspended`, `Canceled`.
- `InventoryMovementType`: `StockEntry`, `StockExit`, `PositiveAdjustment`, `NegativeAdjustment`, `Sale`, `SaleCancellation`, `Purchase`.
- `SaleStatus`: `Completed`, `Cancelled`; `PurchaseStatus`: `Pending`, `Received`, `Cancelled`.
- `PaymentMethod`: `Cash`, `Pix`, `CreditCard`, `DebitCard`.
- `CashSessionStatus`: `Open`, `Closed`; `CashMovementType`: `Opening`, `Sale`, `Withdrawal`, `Supply`, `ClosingAdjustment`.
- `AuditAction`: 23 valores de `Created` a `TenantSettingsUpdated`.

## Commands, requests e responses

### Autenticação

- API: `LoginRequest`; resposta `AuthenticationResponse`.
- Application: `LoginCommand`, `RefreshCommand`, `LogoutCommand`, `AuthenticatedSession`, `SessionContext`, `SessionUser`, `SessionTenant`.
- Cookie interno: `RefreshCookiePayload`.

### Administração e catálogo

- Requests: `SaveUserRequest`, `SaveRoleRequest`, `SaveCategoryRequest`, `SaveProductRequest`.
- Responses: `LookupItem`, `UserItem`, `RoleItem`, `PermissionItem`, `AuditItem`, `PagedAudit`, `CategoryItem`, `ProductItem`, `PagedProducts`.

### Operação

- Estoque: `InventoryItem`, `InventoryMovementItem`, `CreateInventoryMovementRequest`, `InventoryMovementResult`, `PagedResult<T>`.
- Clientes: `SaveCustomerRequest`, `CustomerItem`, `PagedCustomers`.
- Vendas: `CreateSaleItemRequest`, `CreateSaleRequest`, `CancelSaleRequest`, `SaleItemView`, `SaleView`, `SaleListItem`, `PagedSales`.
- Fornecedores/compras: `SaveSupplierRequest`, `SupplierItem`, `PagedSuppliers`, `CreatePurchaseItemRequest`, `CreatePurchaseRequest`, `PurchaseActionRequest`, `PurchaseItemView`, `PurchaseView`, `PurchaseListItem`, `PagedPurchases`.
- Caixa: `OpenCashRequest`, `CashOperationRequest`, `CloseCashRequest`, `CashMovementItem`, `CashSessionView`.
- Analytics: `DashboardView`, `ReportView` e seus itens (`MetricPoint`, `LowStockItem`, `RecentSale`, `TopProduct`, `PaymentSummary`, `InventoryReportItem`, `PurchaseReportItem`, `StockMovementReportItem`), além de `ExportedReport`.
- Settings: `TenantSettingsView`, `UpdateTenantSettingsRequest`, `StoredFile`.

Todos esses contratos são records; controllers recebem requests e retornam os views/paginados correspondentes. Stores projetam entidades para esses modelos, e serviços aplicam validação e contexto do tenant.

## Testes backend

- `Forjix.Domain.Tests`: 11 casos expandidos (theories incluídas).
- `Forjix.Application.Tests`: 12 casos.
- `Forjix.IntegrationTests`: 29 casos; 6 independem do SQL real e 23 requerem bancos CI SQL Server, connection strings externas, seed e `FORJIX_RUN_SQL_TESTS=true`.
- A suíte real cobre migrations, idempotência do seed, login/refresh/logout, autorização, isolamento entre bancos, concorrência de estoque, CRUDs, vendas, caixa, relatórios/exportações e settings.
