# Administration and catalog

## Functional modules

- Users: list, create, edit, activate/deactivate and assign access groups.
- Access groups: list, create, edit and assign or remove granular permissions.
- Audit: filter by period, user, action and entity without exposing credentials or tokens.
- Categories: list, create, edit and deactivate.
- Products: filter, create, edit and deactivate with category, SKU, barcode, prices and minimum stock.

There is no current-stock field in Products. Stock balance will belong to the future inventory module and will be derived from controlled movements.

## Authorization

The Angular application hides routes, menu entries and actions that the current user cannot access. This improves usability, but it is not the security boundary. Every endpoint also requires its corresponding dynamic permission policy in the API.

Management permissions are independent from view permissions. A practical access group should receive both when its users need to open a screen and modify its data.

## Endpoints

| Area | Endpoints |
| --- | --- |
| Users | `GET /api/users`, `POST /api/users`, `PUT /api/users/{id}`, `PATCH /api/users/{id}/status`, `PUT /api/users/{id}/roles` |
| Access groups | `GET /api/roles`, `GET /api/roles/permissions`, `POST /api/roles`, `PUT /api/roles/{id}`, `PUT /api/roles/{id}/permissions` |
| Audit | `GET /api/audit` |
| Categories | `GET /api/categories`, `POST /api/categories`, `PUT /api/categories/{id}`, `DELETE /api/categories/{id}` |
| Products | `GET /api/products`, `POST /api/products`, `PUT /api/products/{id}`, `DELETE /api/products/{id}` |

The `DELETE` operations are non-destructive: they deactivate the record. Product updates require the latest `rowVersion` returned by the API.

## Tenant isolation and validation

The tenant database is selected exclusively from the validated JWT identity. Requests never accept a database, connection string or arbitrary tenant identifier. Duplicate category names, SKUs and barcodes are rejected within the current tenant; inactive or missing categories cannot receive products; price and minimum-stock constraints are validated server-side.
