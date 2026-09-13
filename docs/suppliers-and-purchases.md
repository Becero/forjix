# Suppliers and purchases

Suppliers and purchases live only in the tenant database. Supplier search is server-side and paged. Purchases snapshot product name, SKU and cost.

A purchase starts `Pending`. Receiving it creates `Purchase` inventory movements, changes all balances and writes an audit record in one transaction. A received purchase cannot be cancelled in V1; only pending purchases can be cancelled. Purchase and inventory row versions protect concurrent operations.

Permissions are `suppliers.view/manage` and `purchases.view/manage/receive`. The API is exposed under `/api/suppliers` and `/api/purchases`.
