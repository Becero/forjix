# API e endpoints

## Convenções

- Base relativa: `/api`; controllers usam JSON, salvo upload/download.
- Endpoints protegidos recebem bearer JWT. `RequirePermission` combina autenticação e a permissão indicada.
- Erros de aplicação são convertidos em `ProblemDetails` pelo `ApiExceptionHandler`; `X-Correlation-ID` identifica a requisição.
- Parâmetros sem anotação explícita seguem o binding padrão do ASP.NET Core: tipos complexos no corpo e escalares na rota/query.

## Controllers existentes

| Controller | Área |
|---|---|
| `AuthController` | login, refresh e logout |
| `MeController` | contexto da sessão |
| `AccessProofController` | prova técnica de policy |
| `UsersController`, `RolesController`, `AuditController` | administração e auditoria |
| `CategoriesController`, `ProductsController` | catálogo |
| `InventoryController` | estoque e movimentações |
| `CustomersController` | clientes |
| `SuppliersController`, `PurchasesController` | fornecedores e compras |
| `SalesController` | vendas/PDV |
| `CashController` | caixa |
| `DashboardController`, `ReportsController` | indicadores e relatórios |
| `SettingsController` | configurações e logotipo |

São 17 controllers. Health checks e fallback são mapeados diretamente em `Program.cs`, portanto não têm controller.

## Autenticação e sessão

| Verbo e rota | Request | Response | Autorização | Serviço chamado |
|---|---|---|---|---|
| `POST /api/auth/login` | `LoginRequest` (`TenantSlug`, `Email`, `Password`) | `200 AuthenticationResponse`; cookie refresh; `401` inválido | anônimo; rate limit `authentication` | `IAuthenticationService.LoginAsync` |
| `POST /api/auth/refresh` | cookie de refresh; sem body | `200 AuthenticationResponse` e cookie rotacionado; `401` | anônimo; rate limit | `IAuthenticationService.RefreshAsync` |
| `POST /api/auth/logout` | cookie de refresh; sem body | `204`, cookie removido | anônimo; rate limit | `IAuthenticationService.LogoutAsync` se houver cookie |
| `GET /api/me` | — | `SessionContext` ou `401` | autenticado | `IAuthenticationService.GetSessionContextAsync` |
| `GET /api/access-proof/administration-users` | — | `{ "allowed": true }` | `administration.users.view` | nenhum; prova de policy |

`AuthenticationResponse` contém `AccessToken`, `AccessTokenExpiresAt` e `Context`; o refresh token não é devolvido no JSON.

## Administração e auditoria

| Verbo e rota | Request | Response | Permissão | Serviço |
|---|---|---|---|---|
| `GET /api/users` | — | `IReadOnlyList<UserItem>` | `administration.users.view` | `IManagementService.GetUsersAsync` |
| `POST /api/users` | `SaveUserRequest` | `201 UserItem` | `administration.users.manage` | `CreateUserAsync` |
| `PUT /api/users/{id:guid}` | `SaveUserRequest` | `UserItem` | `administration.users.manage` | `UpdateUserAsync` |
| `GET /api/roles` | — | `IReadOnlyList<RoleItem>` | `administration.roles.view` | `GetRolesAsync` |
| `GET /api/roles/permissions` | — | `IReadOnlyList<PermissionItem>` | `administration.roles.view` | `GetPermissionsAsync` |
| `POST /api/roles` | `SaveRoleRequest` | `201 RoleItem` | `administration.roles.manage` | `CreateRoleAsync` |
| `PUT /api/roles/{id:guid}` | `SaveRoleRequest` | `RoleItem` | `administration.roles.manage` | `UpdateRoleAsync` |
| `GET /api/audit` | query `userId`, `action`, `entity`, `from`, `to`, `page`, `pageSize` | `PagedAudit` | `audit.view` | `GetAuditAsync` |

## Catálogo

| Verbo e rota | Request | Response | Permissão | Serviço |
|---|---|---|---|---|
| `GET /api/categories` | query `includeInactive` (padrão `true`) | lista de `CategoryItem` | `categories.view` | `GetCategoriesAsync` |
| `POST /api/categories` | `SaveCategoryRequest` | `201 CategoryItem` | `categories.manage` | `CreateCategoryAsync` |
| `PUT /api/categories/{id:guid}` | `SaveCategoryRequest` | `CategoryItem` | `categories.manage` | `UpdateCategoryAsync` |
| `DELETE /api/categories/{id:guid}` | — | `CategoryItem` desativado | `categories.manage` | `DeactivateCategoryAsync` |
| `GET /api/products` | query `search`, `categoryId`, `isActive`, `page`, `pageSize` | `PagedProducts` | `products.view` | `GetProductsAsync` |
| `POST /api/products` | `SaveProductRequest` | `201 ProductItem` | `products.manage` | `CreateProductAsync` |
| `PUT /api/products/{id:guid}` | `SaveProductRequest` | `ProductItem` | `products.manage` | `UpdateProductAsync` |
| `DELETE /api/products/{id:guid}` | — | `ProductItem` desativado | `products.manage` | `DeactivateProductAsync` |

## Estoque

| Verbo e rota | Request | Response | Permissão | Serviço |
|---|---|---|---|---|
| `GET /api/inventory` | query `search`, `categoryId`, `status` | lista de `InventoryItem` | `stock.view` | `IInventoryService.GetAsync` |
| `GET /api/inventory/{productId:guid}` | — | `InventoryItem` | `stock.view` | `GetByProductAsync` |
| `GET /api/inventory/{productId:guid}/movements` | query `from`, `to`, `type`, `user`, `page`, `pageSize` | `PagedResult<InventoryMovementItem>` | `stock.view` | `GetMovementsAsync` |
| `POST /api/inventory/{productId:guid}/movements` | `CreateInventoryMovementRequest` | `InventoryMovementResult` | `stock.manage` | `CreateMovementAsync` |

## Clientes, fornecedores e compras

| Verbo e rota | Request | Response | Permissão | Serviço |
|---|---|---|---|---|
| `GET /api/customers` | query `search`, `isActive`, `page`, `pageSize` | `PagedCustomers` | `customers.view` | `ICustomerService.GetAsync` |
| `POST /api/customers` | `SaveCustomerRequest` | `201 CustomerItem` | `customers.manage` | `CreateAsync` |
| `PUT /api/customers/{id:guid}` | `SaveCustomerRequest` | `CustomerItem` | `customers.manage` | `UpdateAsync` |
| `GET /api/suppliers` | query `search`, `isActive`, `page`, `pageSize` | `PagedSuppliers` | `suppliers.view` | `IPurchaseService.GetSuppliersAsync` |
| `POST /api/suppliers` | `SaveSupplierRequest` | `201 SupplierItem` | `suppliers.manage` | `CreateSupplierAsync` |
| `PUT /api/suppliers/{id:guid}` | `SaveSupplierRequest` | `SupplierItem` | `suppliers.manage` | `UpdateSupplierAsync` |
| `GET /api/purchases` | query `search`, `status`, `page`, `pageSize` | `PagedPurchases` | `purchases.view` | `GetPurchasesAsync` |
| `GET /api/purchases/{id:guid}` | — | `PurchaseView` | `purchases.view` | `GetPurchaseAsync` |
| `POST /api/purchases` | `CreatePurchaseRequest` | `201 PurchaseView` | `purchases.manage` | `CreatePurchaseAsync` |
| `POST /api/purchases/{id:guid}/receive` | `PurchaseActionRequest` | `PurchaseView` | `purchases.receive` | `ReceiveAsync` |
| `POST /api/purchases/{id:guid}/cancel` | `PurchaseActionRequest` | `PurchaseView` | `purchases.manage` | `CancelAsync` |

## Vendas e caixa

| Verbo e rota | Request | Response | Permissão | Serviço |
|---|---|---|---|---|
| `GET /api/sales` | query `from`, `to`, `status`, `page`, `pageSize` | `PagedSales` | `sales.view` | `ISalesService.GetAsync` |
| `GET /api/sales/{id:guid}` | — | `SaleView` | `sales.view` | `GetByIdAsync` |
| `POST /api/sales` | header `Idempotency-Key`; `CreateSaleRequest` | `201 SaleView` | `sales.create`; desconto também requer `sales.discount` no caso de uso | `CreateAsync` |
| `POST /api/sales/{id:guid}/cancel` | `CancelSaleRequest` | `SaleView` | `sales.cancel` | `CancelAsync` |
| `GET /api/cash/current` | — | `CashSessionView` ou `null` | `cash.view` | `ICashService.GetCurrentAsync` |
| `POST /api/cash/open` | `OpenCashRequest` | `201 CashSessionView` | `cash.manage` | `OpenAsync` |
| `POST /api/cash/supply` | `CashOperationRequest` | `CashSessionView` | `cash.manage` | `SupplyAsync` |
| `POST /api/cash/withdraw` | `CashOperationRequest` | `CashSessionView` | `cash.manage` | `WithdrawAsync` |
| `POST /api/cash/close` | `CloseCashRequest` | `CashSessionView` | `cash.manage` | `CloseAsync` |

## Dashboard, relatórios e configurações

| Verbo e rota | Request | Response | Permissão | Serviço |
|---|---|---|---|---|
| `GET /api/dashboard` | — | `DashboardView` | `dashboard.view` | `IAnalyticsService.DashboardAsync` |
| `GET /api/reports` | query `from`, `through` | `ReportView` | `reports.view` | `ReportAsync` |
| `GET /api/reports/export` | query `from`, `through`, `format` | arquivo PDF ou XLS | `reports.export` | `ExportAsync` |
| `GET /api/settings` | — | `TenantSettingsView` | `settings.view` | `ISettingsService.GetAsync` |
| `PUT /api/settings` | `UpdateTenantSettingsRequest` | `TenantSettingsView` | `settings.manage` | `UpdateAsync` |
| `POST /api/settings/logo` | multipart `file`, até 2 MB | `204` | `settings.manage` | `UploadLogoAsync` |
| `GET /api/settings/logo` | — | arquivo ou `404` | `settings.view` | `GetLogoAsync` |

## Saúde e fallback

| Verbo e rota | Response | Autorização | Observação |
|---|---|---|---|
| `GET /api/health` | resultado de health checks | anônimo | atualmente não há checks específicos de SQL registrados |
| `GET /api/health/live` | resultado de health checks com predicado vazio | anônimo | liveness do processo |
| qualquer `/api/{**path}` não mapeado | `404 ProblemDetails` | conforme pipeline | fallback explícito |

O OpenAPI é mapeado somente em `Development`; o código não mantém um contrato OpenAPI versionado nem um cliente Angular gerado.
