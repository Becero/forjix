# Frontend Angular

## Estrutura

`web/forjix-web` é uma aplicação Angular 20 standalone. `main.ts` inicializa `App` com `appConfig`; o router e o `HttpClient` são fornecidos funcionalmente. As telas são lazy-loaded com `loadComponent`.

```text
src/app/
├── core/
│   ├── api/          clientes e models HTTP
│   ├── auth/         login, serviço e store de sessão
│   ├── guards/       autenticação e permissão
│   └── interceptors/ bearer/refresh e correlation ID
├── features/         telas por capacidade
└── layouts/          shell autenticado e menu
```

## Rotas

| Rota | Componente | Proteção |
| --- | --- | --- |
| `/login` | `Login` | pública |
| `/app` | `Dashboard` em `AuthenticatedLayout` | `authGuard` + `dashboard.view` |
| `/app/products` | `Products` | `products.view` |
| `/app/categories` | `Categories` | `categories.view` |
| `/app/inventory` | `InventoryList` | `stock.view` |
| `/app/pos` | `Pos` | `sales.create` |
| `/app/sales` | `Sales` | `sales.view` |
| `/app/customers` | `Customers` | `customers.view` |
| `/app/suppliers` | `Suppliers` | `suppliers.view` |
| `/app/purchases` | `Purchases` | `purchases.view` |
| `/app/cash` | `Cash` | `cash.view` |
| `/app/reports` | `Reports` | `reports.view` |
| `/app/settings` | `Settings` | `settings.view` |
| `/app/users` | `Users` | `administration.users.view` |
| `/app/roles` | `Roles` | `administration.roles.view` |
| `/app/audit` | `Audit` | `audit.view` |

Raiz e wildcard redirecionam para `/login`. O `AuthenticatedLayout` só renderiza links cujo código esteja em `SessionContext.permissions`; ações de escrita nas telas também usam permissões `*.manage`, `sales.cancel`, `reports.export` etc. Isso melhora UX, mas a autorização definitiva continua na API.

## Componentes/features

- `Dashboard`: métricas, vendas recentes, estoque baixo e produtos mais vendidos.
- `Products` e `Categories`: filtro/lista, modal de inclusão/edição e desativação.
- Inventário: `InventoryList`, `InventoryHistory` e `InventoryMovementDialog`.
- `Pos`: catálogo pesquisável, carrinho, cliente opcional, desconto condicionado, status do caixa e criação com UUID idempotente.
- `Sales`: filtros, paginação e cancelamento.
- `Customers`, `Suppliers`, `Purchases`: cadastros/listagens e recebimento/cancelamento de compra.
- `Cash`: abertura, suprimento, sangria, fechamento e movimentos.
- `Reports`: período, indicadores e downloads PDF/Excel quando permitido.
- `Users`, `Roles`, `Audit`, `Settings`: administração, permissões, auditoria e dados da empresa/logo.

## Serviços HTTP

| Serviço | Endpoints consumidos |
| --- | --- |
| `AuthService` | `/api/auth/login`, `/refresh`, `/logout` |
| `ManagementApiService` | users, roles, audit, categories e products |
| `InventoryApiService` | inventário e movimentos |
| `SalesApiService` | vendas e cancelamento |
| `CustomersApiService` | clientes |
| `PurchasesApiService` | fornecedores e compras |
| `CashApiService` | sessão e operações de caixa |
| `AnalyticsApiService` | dashboard, relatórios e exportação |
| `SettingsApiService` | settings e upload de logo |

Todos usam caminhos relativos; não há URL de API por environment no código atual.

## Sessão, guards e interceptors

- `AuthSessionStore` mantém access token e contexto somente em signals em memória; não usa local/session storage.
- `authGuard` aceita sessão existente ou tenta refresh por cookie; em falha limpa estado e redireciona ao login.
- `permissionGuard` verifica o código da rota no contexto e redireciona para `/app` se faltar.
- `authInterceptor` adiciona Bearer às chamadas não-auth, inclui credentials e, em 401, compartilha uma única tentativa de refresh (`shareReplay`) antes de repetir a chamada.
- `correlationIdInterceptor` cria um UUID (com fallback timestamp/aleatório) para cada chamada.

## Forms e estado

As telas usam Reactive Forms, signals e subscriptions diretas. Não há biblioteca externa de estado, componentes ou internacionalização. `apiError` extrai `detail`/`title` de Problem Details.

## Build e testes

O build padrão é production, com hashing e budgets (500 kB warning/1 MB error inicial; 4/8 kB por estilo de componente). O proxy de desenvolvimento encaminha `/api` para a API local. Existem apenas 2 testes Jasmine em `app.spec.ts`: criação do app e presença do router outlet.
