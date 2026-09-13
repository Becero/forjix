using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Features.Management;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Catalog;
using Forjix.Domain.Entities.Identity;

namespace Forjix.Application.Abstractions.Management;

public interface IManagementStoreFactory
{
    IManagementStore Create(ResolvedTenantDatabase tenant);
}

public interface IManagementStore : IAsyncDisposable
{
    Task<List<User>> GetUsersAsync(CancellationToken cancellationToken);
    Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> RolesExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task AddUserAsync(User user, IReadOnlyCollection<Guid> roleIds, AuditLog audit, CancellationToken cancellationToken);
    Task SaveUserAsync(User user, IReadOnlyCollection<Guid> roleIds, AuditLog audit, CancellationToken cancellationToken);

    Task<List<Role>> GetRolesAsync(CancellationToken cancellationToken);
    Task<Role?> GetRoleAsync(Guid id, CancellationToken cancellationToken);
    Task<List<Permission>> GetPermissionsAsync(CancellationToken cancellationToken);
    Task<bool> RoleNameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> PermissionsExistAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken);
    Task AddRoleAsync(Role role, IReadOnlyCollection<string> permissions, AuditLog audit, CancellationToken cancellationToken);
    Task SaveRoleAsync(Role role, IReadOnlyCollection<string> permissions, AuditLog audit, CancellationToken cancellationToken);

    Task<List<AuditItem>> GetAuditAsync(Guid? userId, string? action, string? entity, DateTimeOffset? from, DateTimeOffset? toDate, CancellationToken cancellationToken);
    Task<List<Category>> GetCategoriesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<Category?> GetCategoryAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CategoryNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken);
    Task AddCategoryAsync(Category category, AuditLog audit, CancellationToken cancellationToken);
    Task SaveCategoryAsync(Category category, AuditLog audit, CancellationToken cancellationToken);

    Task<List<Product>> GetProductsAsync(string? search, Guid? categoryId, bool? isActive, CancellationToken cancellationToken);
    Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ProductSkuExistsAsync(string sku, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> ProductBarcodeExistsAsync(string barcode, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> CategoryIsActiveAsync(Guid id, CancellationToken cancellationToken);
    Task AddProductAsync(Product product, AuditLog audit, CancellationToken cancellationToken);
    Task SaveProductAsync(Product product, byte[]? expectedRowVersion, AuditLog audit, CancellationToken cancellationToken);
}
