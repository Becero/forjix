namespace Forjix.Application.Features.Management;

public sealed record LookupItem(Guid Id, string Name);
public sealed record UserItem(Guid Id, string Name, string Email, bool IsActive, DateTimeOffset CreatedAt, IReadOnlyList<LookupItem> Groups);
public sealed record SaveUserRequest(string Name, string Email, string? Password, bool IsActive, IReadOnlyList<Guid> RoleIds);
public sealed record RoleItem(Guid Id, string Name, string? Description, bool IsSystem, DateTimeOffset CreatedAt, IReadOnlyList<string> Permissions);
public sealed record SaveRoleRequest(string Name, string? Description, IReadOnlyList<string> Permissions);
public sealed record PermissionItem(string Code, string Name, string Module);
public sealed record AuditItem(long Id, DateTimeOffset OccurredAt, string? UserName, string Action, string Entity, string? EntityId, string? Details);
public sealed record CategoryItem(Guid Id, string Name, string? Description, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record SaveCategoryRequest(string Name, string? Description, bool IsActive);
public sealed record ProductItem(Guid Id, Guid CategoryId, string CategoryName, string Name, string Sku, string? Barcode, decimal SalePrice, decimal CostPrice, decimal MinimumStock, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string RowVersion);
public sealed record SaveProductRequest(Guid CategoryId, string Name, string Sku, string? Barcode, decimal SalePrice, decimal CostPrice, decimal MinimumStock, bool IsActive, string? RowVersion);
