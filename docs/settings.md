# Tenant settings

Safe company settings live in `ForjixMaster`; operational entities remain in the isolated tenant database. The module exposes trade/legal name, CNPJ, contacts, address, negative-stock policy, BRL currency and timezone under `/api/settings`, protected by `settings.view/manage`.

Logos are limited to 2 MB and PNG, JPEG or WebP. `IFileStorage` keeps only a storage key in settings. Development uses local files under the application data directory and production can replace the provider with Blob/S3-compatible storage. Updates generate a compact tenant audit entry without storing full commercial data.
