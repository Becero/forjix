# Customers

Customers are optional sale contacts stored only in each tenant database. Search and status filtering are server-side and paged. A sale may remain an unidentified consumer.

Documents are normalized to digits, accept 11 or 14 digits and are unique when provided. Customer writes generate compact audit records. Endpoints `GET /api/customers`, `POST /api/customers` and `PUT /api/customers/{id}` require `customers.view` or `customers.manage` as appropriate.
