# Sales and point of sale

Sales are tenant-owned commercial records. `SaleItem` stores product name, SKU, unit price and unit cost snapshots, so later catalog changes never rewrite history.

## Consistency

- `POST /api/sales` requires an `Idempotency-Key` header. Its unique database index prevents duplicate sales.
- Daily sale numbers use `SaleSequence` inside a serializable transaction; they never use `COUNT + 1`.
- The server reloads product prices and costs and recalculates subtotal and total.
- Creating or cancelling a sale writes the sale, inventory balances, `InventoryMovement` records and a compact `AuditLog` atomically.
- Inventory is never updated directly by the sales feature.
- `RowVersion` protects cancellation and inventory rows protect concurrent checkout.

## Endpoints and permissions

- `GET /api/sales` and `GET /api/sales/{id}`: `sales.view`.
- `POST /api/sales`: `sales.create`; a positive discount additionally requires `sales.discount`.
- `POST /api/sales/{id}/cancel`: `sales.cancel`.

Payment methods are managerial only (`Cash`, `Pix`, `CreditCard`, `DebitCard`). No card data or external payment credentials are collected.
