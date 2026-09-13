# Módulos do sistema

Esta classificação reflete somente funcionalidades presentes no código atual. “Implementado” significa que há persistência e caso de uso/API; não implica maturidade de produto para produção.

| Módulo solicitado | Situação verificada | Implementação existente |
|---|---|---|
| Administração | Implementado | usuários, grupos/papéis, permissões e auditoria |
| Catálogo | Implementado | categorias e produtos |
| Produtos | Implementado | consulta paginada, criação, edição e desativação |
| Categorias | Implementado | consulta, criação, edição e desativação |
| Estoque | Implementado | saldo por produto, filtros e custo de estoque |
| Movimentações | Implementado | entrada, saída e ajustes manuais; movimentos automáticos por venda, cancelamento e compra |
| Clientes | Implementado | consulta paginada, criação e edição |
| Fornecedores | Implementado | consulta paginada, criação e edição |
| Compras | Implementado | criação, consulta, recebimento e cancelamento |
| Vendas | Implementado | criação idempotente, consulta e cancelamento |
| PDV | Implementado no frontend sobre Vendas | tela `PosComponent`; usa catálogo e `POST /api/sales` |
| Caixa | Implementado | sessão atual, abertura, suprimento, sangria e fechamento |
| Dashboard | Implementado | indicadores, últimos sete dias, estoque baixo, vendas recentes e produtos mais vendidos |
| Relatórios | Implementado | período, vendas, pagamentos, produtos, estoque, compras e movimentos; exportação PDF ou Excel XML |
| Configurações | Implementado | dados do estabelecimento, estoque negativo, moeda, fuso e logotipo |

## Administração

`ManagementService` concentra usuários, papéis, permissões, auditoria, categorias e produtos; `ManagementStore` realiza a persistência. Usuário pode pertencer a vários papéis por `UserRoles`; papel recebe permissões por `RolePermissions`. O menu Angular usa as mesmas permissões para ocultar áreas sem acesso, enquanto a API efetivamente aplica a autorização.

Operações administrativas relevantes geram `AuditLog`. A auditoria aceita filtros de usuário, ação, entidade e intervalo de datas.

## Catálogo, produtos e categorias

Cada `Product` pertence a uma `Category`. SKU e código de barras, quando presente, são únicos no banco do tenant. Preço de venda, custo e estoque mínimo não aceitam valores negativos. A remoção exposta pela API é desativação lógica (`IsActive`), não exclusão física.

## Estoque e movimentações

`Inventory` mantém um saldo único por produto. `InventoryMovement` registra saldo anterior/novo, quantidade, usuário, motivo e eventual referência externa. `InventoryService` executa movimentações manuais com transação `Serializable` e concorrência otimista por `RowVersion` quando fornecida.

Vendas baixam estoque; cancelamentos de venda o devolvem; recebimentos de compra dão entrada. A configuração `AllowNegativeStock` decide se a operação pode deixar saldo negativo.

## Clientes

`CustomerService` oferece busca por texto/status, paginação, criação e atualização. O cliente é opcional na venda. Documento, e-mail, telefone e observações são opcionais.

## Fornecedores e compras

Uma `Purchase` pertence a um fornecedor e contém `PurchaseItems`. Ao criar, fica `Pending`; o recebimento muda para `Received` e atualiza estoque; o cancelamento muda uma compra pendente para `Cancelled`. O `RowVersion` da compra protege as transições concorrentes.

## Vendas e PDV

`SalesService` valida itens, preços, desconto e permissão específica para desconto. A requisição exige o cabeçalho `Idempotency-Key`; o valor é armazenado com índice único, evitando duplicidade no mesmo banco de tenant. A numeração usa `SaleSequence` anual.

O PDV é uma composição frontend, não um controller separado: `PosComponent` consulta produtos e clientes e cria a venda por `SalesApiService`.

## Caixa

O modelo contém `CashRegister`, `CashSession` e `CashMovement`. O serviço trabalha com o caixa padrão, impede mais de uma sessão aberta pela regra transacional, calcula o valor esperado a partir da abertura, vendas em dinheiro, suprimentos e sangrias, e registra fechamento e auditoria.

## Dashboard e relatórios

`AnalyticsService` lê projeções de vendas, produtos, inventário, compras, movimentos e caixa por meio de `IAnalyticsStore`. O relatório aceita datas opcionais e retorna `ReportView`. A exportação atual produz:

- PDF gerado pelo próprio serviço, com conteúdo textual resumido;
- planilha SpreadsheetML 2003 servida como `.xls`, não um arquivo Open XML `.xlsx`.

## Configurações

`SettingsService` combina dados do `Tenant` no Master com chaves de `TenantSettings`. O logotipo é gravado por `LocalTenantAssetStore` no filesystem em `App_Data/tenant-assets` por padrão, com limite HTTP de 2 MB e formatos PNG, JPEG ou WebP.

## Funcionalidades não encontradas

Não há módulos de emissão fiscal, TEF/adquirência, aplicativo móvel, multi-loja, pedidos de venda independentes, contas a pagar/receber ou e-commerce. Eles também não devem ser inferidos a partir das telas atuais.
