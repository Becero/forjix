# Inventory and stock movements

## Model

`Product` remains the catalog aggregate and does not store physical balance. `Inventory` is the current stock state and has a one-to-one relation with Product in this single-unit version. `InventoryMovement` is the immutable operational history of every balance change.

Every new product receives an Inventory with quantity zero in the same save operation. The inventory migration safely creates zero balances for products that already exist.

The one-to-one constraint is intentionally centered on `ProductId`. A future multi-store phase may evolve its uniqueness to `(StoreId, ProductId)` without moving balance into Product.

## Movement types

- `StockEntry`: adds the positive request quantity.
- `StockExit`: subtracts the positive request quantity.
- `PositiveAdjustment`: adds quantity and requires a reason.
- `NegativeAdjustment`: subtracts quantity and requires a reason.

The API never accepts a signed quantity or a desired final balance. The command carries the last Inventory `rowVersion` seen by the operator so stale screens fail deterministically. Future sale, cancellation, purchase and return concepts are not implemented in this phase.

## Negative stock

`AllowNegativeStock` comes from the trusted tenant settings resolved through the authenticated tenant. Its default is `false`. The Angular preview improves usability, while the API is the enforcement boundary.

## Transaction and concurrency

Inventory uses SQL Server `rowversion`. Each movement updates Inventory and inserts InventoryMovement plus a compact AuditLog in one database transaction. A concurrent stale update fails with HTTP 409 and is not retried automatically. Any failure rolls the complete transaction back.

InventoryMovement is never deleted by the normal application flow. Audit records contain only ProductId, movement type and identifiers, not the complete operational payload.

## API

| Permission | Endpoint | Purpose |
| --- | --- | --- |
| `stock.view` | `GET /api/inventory` | Filter balances by text, category and status |
| `stock.view` | `GET /api/inventory/{productId}` | Get one current balance |
| `stock.view` | `GET /api/inventory/{productId}/movements` | Filter and page movement history |
| `stock.manage` | `POST /api/inventory/{productId}/movements` | Create entry, exit or adjustment |

The single POST endpoint uses a closed movement type and keeps validation, sign calculation, concurrency and auditing in one use case. Inactive and missing products are rejected. Quantity must be greater than zero.

## Status and cost queries

- Negative: quantity below zero.
- Out of stock: quantity equals zero.
- Low: quantity is positive and at or below Product.MinimumStock.
- Normal: quantity is above Product.MinimumStock.

Stock cost is not persisted. A future reusable summary query should calculate `Inventory.Quantity * Product.CostPrice` and explicitly define whether negative quantities contribute zero or a negative value before exposing a commercial dashboard.
