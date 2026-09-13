# Dicionário de dados

Fonte: snapshots EF Core 10.0.4 e configurações atuais. Convenções: **NN** = `NOT NULL`; **NULL** = anulável; **PK** = primary key; **UQ** = índice único. Enums são persistidos como `int`. Não há schemas SQL customizados nem check constraints declarados.

## ForjixMaster

### `Tenants`

| Coluna | SQL/null | Papel |
| --- | --- | --- |
| `Id` | `uniqueidentifier` NN | PK |
| `Name` | `nvarchar(160)` NN | Nome da empresa |
| `Slug` | `nvarchar(80)` NN | Identificador de login, UQ |
| `Cnpj` | `nvarchar(14)` NULL | UQ filtrado quando não nulo |
| `Status` | `int` NN | `TenantStatus` |
| `PlanId` | `uniqueidentifier` NULL | FK → `Plans.Id`, `SET NULL` |
| `CreatedAt`, `UpdatedAt` | `datetimeoffset` NN | Auditoria temporal |

Índices: UQ `Slug`; UQ filtrado `Cnpj`; índice FK `PlanId` criado por convenção.

### `Plans`

`Id uniqueidentifier` NN PK; `Code nvarchar(50)` NN UQ; `Name nvarchar(120)` NN; `IsActive bit` NN; `UserLimit int` NN; `CreatedAt/UpdatedAt datetimeoffset` NN.

### `Subscriptions`

`Id uniqueidentifier` NN PK; `TenantId uniqueidentifier` NN FK → `Tenants` RESTRICT; `PlanId uniqueidentifier` NN FK → `Plans` RESTRICT; `Status int` NN; `StartsAt datetimeoffset` NN; `EndsAt datetimeoffset` NULL; `CreatedAt/UpdatedAt datetimeoffset` NN. Índices: `(TenantId, Status)` e `PlanId`.

### `TenantDatabases`

`Id uniqueidentifier` NN PK; `TenantId uniqueidentifier` NN FK/UQ → `Tenants` CASCADE; `DatabaseName nvarchar(128)` NN; `ServerReference nvarchar(200)` NN; `SecretReference nvarchar(300)` NN; `SchemaVersion nvarchar(160)` NN; `CreatedAt/UpdatedAt datetimeoffset` NN; `LastMigratedAt datetimeoffset` NULL. A UQ em `TenantId` materializa 1:1.

### `TenantSettings`

`Id uniqueidentifier` NN PK; `TenantId uniqueidentifier` NN FK → `Tenants` CASCADE; `Key nvarchar(120)` NN; `Value nvarchar(2000)` NN; `UpdatedAt datetimeoffset` NN. UQ composta `(TenantId, Key)`.

### `TenantFeatures`

`Id uniqueidentifier` NN PK; `TenantId uniqueidentifier` NN FK → `Tenants` CASCADE; `FeatureCode nvarchar(120)` NN; `IsEnabled bit` NN; `ExpiresAt datetimeoffset` NULL; `UpdatedAt datetimeoffset` NN. UQ `(TenantId, FeatureCode)`.

### `MigrationExecutions`

`Id uniqueidentifier` NN PK; `TenantId uniqueidentifier` NULL FK → `Tenants` SET NULL; `DatabaseName nvarchar(128)` NN; `MigrationType nvarchar(30)` NN; `Status nvarchar(30)` NN; `AppliedMigration nvarchar(160)` NULL; `ErrorSummary nvarchar(1000)` NULL; `StartedAt/CompletedAt datetimeoffset` NN. Índice `(TenantId, StartedAt)`.

## Banco de cada tenant

### Identidade e auditoria

#### `Users`

`Id uniqueidentifier` NN PK; `Name nvarchar(160)` NN; `Email nvarchar(254)` NN; `NormalizedEmail nvarchar(254)` NN UQ; `PasswordHash nvarchar(500)` NN; `IsActive bit` NN; `LockedUntil datetimeoffset` NULL; `FailedAccessAttempts int` NN; `SecurityStamp uniqueidentifier` NN; `CreatedAt/UpdatedAt datetimeoffset` NN.

#### `Roles`

`Id uniqueidentifier` NN PK; `Name nvarchar(120)` NN; `NormalizedName nvarchar(120)` NN UQ; `Description nvarchar(500)` NULL; `IsSystem bit` NN; `CreatedAt/UpdatedAt datetimeoffset` NN.

#### `Permissions`

`Id uniqueidentifier` NN PK; `Code nvarchar(160)` NN UQ; `Name nvarchar(160)` NN; `Description nvarchar(500)` NULL; `Module nvarchar(100)` NN.

#### `UserRoles`

`UserId uniqueidentifier` NN e `RoleId uniqueidentifier` NN formam a PK. FKs: `UserId` → `Users` CASCADE; `RoleId` → `Roles` CASCADE. `AssignedAt datetimeoffset` NN. Índice adicional em `RoleId`.

#### `RolePermissions`

`RoleId uniqueidentifier` NN e `PermissionId uniqueidentifier` NN formam a PK. FKs para `Roles` e `Permissions`, ambas CASCADE. `GrantedAt datetimeoffset` NN. Índice adicional em `PermissionId`.

#### `RefreshTokens`

`Id uniqueidentifier` NN PK; `UserId uniqueidentifier` NN FK → `Users` CASCADE; `TokenHash nvarchar(128)` NN UQ; `FamilyId uniqueidentifier` NN; `ExpiresAt/CreatedAt datetimeoffset` NN; `RevokedAt datetimeoffset` NULL; `ReplacedByTokenId uniqueidentifier` NULL; `RowVersion rowversion` NN/concurrency token. Índice `(UserId, FamilyId)`. Não há FK self-reference configurada em `ReplacedByTokenId`.

#### `AuditLogs`

`Id bigint IDENTITY` NN PK; `UserId uniqueidentifier` NULL; `Action int` NN; `EntityName nvarchar(160)` NN; `EntityId nvarchar(100)` NULL; `BeforeData/AfterData nvarchar(max)` NULL; `IpAddress nvarchar(64)` NULL; `CorrelationId nvarchar(100)` NN; `OccurredAt datetimeoffset` NN. Índices em `OccurredAt` e `CorrelationId`. `UserId` não possui FK no modelo.

### Catálogo, clientes e estoque

#### `Categories`

`Id uniqueidentifier` NN PK; `Name nvarchar(120)` NN UQ; `Description nvarchar(500)` NULL; `IsActive bit` NN; `CreatedAt/UpdatedAt datetimeoffset` NN.

#### `Products`

`Id uniqueidentifier` NN PK; `CategoryId uniqueidentifier` NN FK → `Categories` RESTRICT; `Name nvarchar(160)` NN; `Sku nvarchar(80)` NN UQ; `Barcode nvarchar(80)` NULL UQ filtrado; `SalePrice/CostPrice decimal(18,2)` NN; `MinimumStock decimal(18,3)` NN; `IsActive bit` NN; `CreatedAt/UpdatedAt datetimeoffset` NN; `RowVersion rowversion` NN. Índice adicional em `CategoryId`.

#### `Inventories`

`Id uniqueidentifier` NN PK; `ProductId uniqueidentifier` NN FK/UQ → `Products` RESTRICT; `Quantity decimal(18,3)` NN; `UpdatedAt datetimeoffset` NN; `RowVersion rowversion` NN. UQ `ProductId` materializa 1:1.

#### `InventoryMovements`

`Id bigint IDENTITY` NN PK; `InventoryId uniqueidentifier` NN FK → `Inventories` RESTRICT; `ProductId uniqueidentifier` NN FK → `Products` RESTRICT; `Type int` NN; `Quantity`, `PreviousQuantity`, `NewQuantity decimal(18,3)` NN; `Reason nvarchar(500)` NULL; `ReferenceType nvarchar(80)` NULL; `ReferenceId nvarchar(100)` NULL; `UserId uniqueidentifier` NN FK → `Users` RESTRICT; `CreatedAt datetimeoffset` NN. Índices: `InventoryId`, `UserId`, `(ProductId, CreatedAt)`.

#### `Customers`

`Id uniqueidentifier` NN PK; `Name nvarchar(160)` NN; `Document nvarchar(14)` NULL UQ filtrado; `Email nvarchar(254)` NULL; `Phone nvarchar(30)` NULL; `Notes nvarchar(1000)` NULL; `IsActive bit` NN; `CreatedAt/UpdatedAt datetimeoffset` NN. Índice não único em `Name`.

### Vendas

#### `Sales`

`Id uniqueidentifier` NN PK; `Number nvarchar(32)` NN UQ; `IdempotencyKey nvarchar(100)` NN UQ; `Status int` NN; `Subtotal`, `Discount`, `Total decimal(18,2)` NN; `PaymentMethod int` NN; `UserId uniqueidentifier` NN FK → `Users` RESTRICT; `CustomerId uniqueidentifier` NULL FK → `Customers` RESTRICT; `CreatedAt datetimeoffset` NN; `CancelledAt datetimeoffset` NULL; `CancelledByUserId uniqueidentifier` NULL; `CancellationReason nvarchar(500)` NULL; `RowVersion rowversion` NN. Índices em `CreatedAt`, `UserId`, `CustomerId`. `CancelledByUserId` não possui FK configurada.

#### `SaleItems`

`Id uniqueidentifier` NN PK; `SaleId uniqueidentifier` NN FK → `Sales` CASCADE; `ProductId uniqueidentifier` NN FK → `Products` RESTRICT; `ProductName nvarchar(160)` NN; `Sku nvarchar(80)` NN; `Quantity decimal(18,3)` NN; `UnitPrice`, `UnitCost`, `Discount`, `Total decimal(18,2)` NN. Índices em `SaleId` e `ProductId`.

#### `SaleSequences`

`Date date` NN PK; `LastValue int` NN.

### Fornecedores e compras

#### `Suppliers`

`Id uniqueidentifier` NN PK; `Name nvarchar(160)` NN; `Document nvarchar(14)` NULL UQ filtrado; `Email nvarchar(254)` NULL; `Phone nvarchar(30)` NULL; `ContactName nvarchar(160)` NULL; `Notes nvarchar(1000)` NULL; `IsActive bit` NN; `CreatedAt/UpdatedAt datetimeoffset` NN. Índice em `Name`.

#### `Purchases`

`Id uniqueidentifier` NN PK; `Number nvarchar(32)` NN UQ; `SupplierId uniqueidentifier` NN FK → `Suppliers` RESTRICT; `Status int` NN; `Total decimal(18,2)` NN; `Notes nvarchar(1000)` NULL; `UserId uniqueidentifier` NN FK → `Users` RESTRICT; `CreatedAt datetimeoffset` NN; `ReceivedAt/CancelledAt datetimeoffset` NULL; `RowVersion rowversion` NN. Índices em `CreatedAt`, `SupplierId` e `UserId`.

#### `PurchaseItems`

`Id uniqueidentifier` NN PK; `PurchaseId uniqueidentifier` NN FK → `Purchases` CASCADE; `ProductId uniqueidentifier` NN FK → `Products` RESTRICT; `ProductName nvarchar(160)` NN; `Sku nvarchar(80)` NN; `Quantity decimal(18,3)` NN; `UnitCost/Total decimal(18,2)` NN. Índices em `PurchaseId` e `ProductId`.

#### `PurchaseSequences`

`Date date` NN PK; `LastValue int` NN.

### Caixa

#### `CashRegisters`

`Id uniqueidentifier` NN PK; `Name nvarchar(100)` NN; `IsActive bit` NN. Não há índice único de nome.

#### `CashSessions`

`Id uniqueidentifier` NN PK; `CashRegisterId uniqueidentifier` NN FK → `CashRegisters` RESTRICT; `OpenedByUserId uniqueidentifier` NN; `OpenedAt datetimeoffset` NN; `OpeningAmount decimal(18,2)` NN; `ClosedByUserId uniqueidentifier` NULL; `ClosedAt datetimeoffset` NULL; `ClosingAmount decimal(18,2)` NULL; `Status int` NN; `RowVersion rowversion` NN. Índices em `CashRegisterId` e `OpenedAt`. Os campos de usuário não têm FKs configuradas.

#### `CashMovements`

`Id bigint IDENTITY` NN PK; `CashSessionId uniqueidentifier` NN FK → `CashSessions` RESTRICT; `Type int` NN; `Amount decimal(18,2)` NN; `Reason nvarchar(500)` NULL; `UserId uniqueidentifier` NN; `CreatedAt datetimeoffset` NN; `SaleId uniqueidentifier` NULL. Índice `(CashSessionId, CreatedAt)`. `UserId` e `SaleId` não têm FKs configuradas.

## Tabela técnica de migrations

O código chama `Database.MigrateAsync()`, portanto o provider EF mantém histórico de migrations em cada banco. O nome convencional é `__EFMigrationsHistory`, mas essa tabela não é uma entidade nem aparece nos snapshots versionados; detalhes internos de DDL não são afirmados aqui para manter a documentação limitada ao código do repositório.
