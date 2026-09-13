namespace Forjix.Application.Features.Management;

public interface IManagementService
{
    Task<IReadOnlyList<UserItem>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserItem> CreateUserAsync(SaveUserRequest request, CancellationToken cancellationToken = default);
    Task<UserItem> UpdateUserAsync(Guid id, SaveUserRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleItem>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionItem>> GetPermissionsAsync(CancellationToken cancellationToken = default);
    Task<RoleItem> CreateRoleAsync(SaveRoleRequest request, CancellationToken cancellationToken = default);
    Task<RoleItem> UpdateRoleAsync(Guid id, SaveRoleRequest request, CancellationToken cancellationToken = default);
    Task<PagedAudit> GetAuditAsync(Guid? userId, string? action, string? entity, DateTimeOffset? from, DateTimeOffset? toDate, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryItem>> GetCategoriesAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<CategoryItem> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryItem> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryItem> DeactivateCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedProducts> GetProductsAsync(string? search, Guid? categoryId, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ProductItem> CreateProductAsync(SaveProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductItem> UpdateProductAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductItem> DeactivateProductAsync(Guid id, CancellationToken cancellationToken = default);
}
