using System.Text.Json;
using Forjix.Application.Abstractions.Authentication;
using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Management;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Catalog;
using Forjix.Domain.Entities.Identity;
using Forjix.Domain.Enums;
using InventoryEntity = Forjix.Domain.Entities.Inventory.Inventory;

namespace Forjix.Application.Features.Management;

internal sealed class ManagementService(
    ITenantDatabaseResolver tenantResolver,
    IManagementStoreFactory storeFactory,
    ICurrentUser currentUser,
    IPasswordHashService passwords,
    TimeProvider timeProvider) : IManagementService
{
    public async Task<IReadOnlyList<UserItem>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var store = await StoreAsync(cancellationToken);
        return (await store.GetUsersAsync(cancellationToken)).Select(MapUser).ToArray();
    }

    public async Task<UserItem> CreateUserAsync(SaveUserRequest request, CancellationToken cancellationToken = default)
    {
        ValidateUser(request, true);
        await using var store = await StoreAsync(cancellationToken);
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        if (await store.EmailExistsAsync(normalizedEmail, null, cancellationToken))
        {
            throw new ResourceConflictException("Já existe um usuário com este e-mail.");
        }

        var roleIds = Distinct(request.RoleIds);
        if (!await store.RolesExistAsync(roleIds, cancellationToken))
        {
            throw new RequestValidationException("Um ou mais grupos de acesso são inválidos.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = string.Empty,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwords.HashPassword(user, request.Password!);
        await store.AddUserAsync(user, roleIds, Audit(AuditAction.Created, nameof(User), user.Id, new { user.Name, user.Email, user.IsActive, RoleIds = roleIds }), cancellationToken);
        return MapUser(user);
    }

    public async Task<UserItem> UpdateUserAsync(Guid id, SaveUserRequest request, CancellationToken cancellationToken = default)
    {
        ValidateUser(request, false);
        await using var store = await StoreAsync(cancellationToken);
        var user = await store.GetUserAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Usuário não encontrado.");
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        if (await store.EmailExistsAsync(normalizedEmail, id, cancellationToken))
        {
            throw new ResourceConflictException("Já existe um usuário com este e-mail.");
        }

        var roleIds = Distinct(request.RoleIds);
        if (!await store.RolesExistAsync(roleIds, cancellationToken))
        {
            throw new RequestValidationException("Um ou mais grupos de acesso são inválidos.");
        }

        var before = new { user.Name, user.Email, user.IsActive, RoleIds = user.UserRoles.Select(x => x.RoleId).ToArray() };
        user.Name = request.Name.Trim();
        user.Email = email;
        user.NormalizedEmail = normalizedEmail;
        user.IsActive = request.IsActive;
        user.UpdatedAt = timeProvider.GetUtcNow();
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            EnsurePassword(request.Password);
            user.PasswordHash = passwords.HashPassword(user, request.Password);
            user.SecurityStamp = Guid.NewGuid();
        }

        await store.SaveUserAsync(user, roleIds, Audit(AuditAction.Updated, nameof(User), user.Id,
            new { Before = before, After = new { user.Name, user.Email, user.IsActive, RoleIds = roleIds } }), cancellationToken);
        return MapUser(user);
    }

    public async Task<IReadOnlyList<RoleItem>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        await using var store = await StoreAsync(cancellationToken);
        return (await store.GetRolesAsync(cancellationToken)).Select(MapRole).ToArray();
    }

    public async Task<IReadOnlyList<PermissionItem>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        await using var store = await StoreAsync(cancellationToken);
        return (await store.GetPermissionsAsync(cancellationToken))
            .Select(x => new PermissionItem(x.Code, x.Name, x.Module)).ToArray();
    }

    public Task<RoleItem> CreateRoleAsync(SaveRoleRequest request, CancellationToken cancellationToken = default) =>
        SaveRoleAsync(null, request, cancellationToken);

    public Task<RoleItem> UpdateRoleAsync(Guid id, SaveRoleRequest request, CancellationToken cancellationToken = default) =>
        SaveRoleAsync(id, request, cancellationToken);

    public async Task<IReadOnlyList<AuditItem>> GetAuditAsync(Guid? userId, string? action, string? entity, DateTimeOffset? from, DateTimeOffset? toDate, CancellationToken cancellationToken = default)
    {
        if (from > toDate) throw new RequestValidationException("O período inicial deve ser anterior ao período final.");
        await using var store = await StoreAsync(cancellationToken);
        return await store.GetAuditAsync(userId, Clean(action), Clean(entity), from, toDate, cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryItem>> GetCategoriesAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        await using var store = await StoreAsync(cancellationToken);
        return (await store.GetCategoriesAsync(includeInactive, cancellationToken)).Select(MapCategory).ToArray();
    }

    public Task<CategoryItem> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default) =>
        SaveCategoryAsync(null, request, cancellationToken);

    public Task<CategoryItem> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default) =>
        SaveCategoryAsync(id, request, cancellationToken);

    public async Task<CategoryItem> DeactivateCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var store = await StoreAsync(cancellationToken);
        var category = await store.GetCategoryAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Categoria não encontrada.");
        category.IsActive = false;
        category.UpdatedAt = timeProvider.GetUtcNow();
        await store.SaveCategoryAsync(category, Audit(AuditAction.Updated, nameof(Category), id, new { category.Name, IsActive = false }), cancellationToken);
        return MapCategory(category);
    }

    public async Task<IReadOnlyList<ProductItem>> GetProductsAsync(string? search, Guid? categoryId, bool? isActive, CancellationToken cancellationToken = default)
    {
        await using var store = await StoreAsync(cancellationToken);
        return (await store.GetProductsAsync(Clean(search), categoryId, isActive, cancellationToken)).Select(MapProduct).ToArray();
    }

    public Task<ProductItem> CreateProductAsync(SaveProductRequest request, CancellationToken cancellationToken = default) =>
        SaveProductAsync(null, request, cancellationToken);

    public Task<ProductItem> UpdateProductAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken = default) =>
        SaveProductAsync(id, request, cancellationToken);

    public async Task<ProductItem> DeactivateProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var store = await StoreAsync(cancellationToken);
        var product = await store.GetProductAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Produto não encontrado.");
        product.IsActive = false;
        product.UpdatedAt = timeProvider.GetUtcNow();
        await store.SaveProductAsync(product, null, Audit(AuditAction.Updated, nameof(Product), id, new { product.Name, IsActive = false }), cancellationToken);
        return MapProduct(product);
    }

    private async Task<RoleItem> SaveRoleAsync(Guid? id, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        var name = Required(request.Name, "O nome do grupo é obrigatório.", 120);
        var permissionCodes = request.Permissions.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
        await using var store = await StoreAsync(cancellationToken);
        var normalizedName = name.ToUpperInvariant();
        if (await store.RoleNameExistsAsync(normalizedName, id, cancellationToken))
            throw new ResourceConflictException("Já existe um grupo com este nome.");
        if (!await store.PermissionsExistAsync(permissionCodes, cancellationToken))
            throw new RequestValidationException("Uma ou mais permissões são inválidas.");

        var now = timeProvider.GetUtcNow();
        if (id is null)
        {
            var role = new Role { Id = Guid.NewGuid(), Name = name, NormalizedName = normalizedName, Description = Clean(request.Description), IsSystem = false, CreatedAt = now, UpdatedAt = now };
            await store.AddRoleAsync(role, permissionCodes, Audit(AuditAction.Created, nameof(Role), role.Id, new { role.Name, role.Description, Permissions = permissionCodes }), cancellationToken);
            return MapRole(role);
        }

        var existing = await store.GetRoleAsync(id.Value, cancellationToken) ?? throw new ResourceNotFoundException("Grupo de acesso não encontrado.");
        existing.Name = name;
        existing.NormalizedName = normalizedName;
        existing.Description = Clean(request.Description);
        existing.UpdatedAt = now;
        await store.SaveRoleAsync(existing, permissionCodes, Audit(AuditAction.PermissionChanged, nameof(Role), existing.Id, new { existing.Name, existing.Description, Permissions = permissionCodes }), cancellationToken);
        return MapRole(existing);
    }

    private async Task<CategoryItem> SaveCategoryAsync(Guid? id, SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        var name = Required(request.Name, "O nome da categoria é obrigatório.", 120);
        await using var store = await StoreAsync(cancellationToken);
        if (await store.CategoryNameExistsAsync(name, id, cancellationToken))
            throw new ResourceConflictException("Já existe uma categoria com este nome.");
        var now = timeProvider.GetUtcNow();
        if (id is null)
        {
            var category = new Category { Id = Guid.NewGuid(), Name = name, Description = Clean(request.Description), IsActive = request.IsActive, CreatedAt = now, UpdatedAt = now };
            await store.AddCategoryAsync(category, Audit(AuditAction.Created, nameof(Category), category.Id, new { category.Name, category.Description, category.IsActive }), cancellationToken);
            return MapCategory(category);
        }

        var existing = await store.GetCategoryAsync(id.Value, cancellationToken) ?? throw new ResourceNotFoundException("Categoria não encontrada.");
        existing.Name = name;
        existing.Description = Clean(request.Description);
        existing.IsActive = request.IsActive;
        existing.UpdatedAt = now;
        await store.SaveCategoryAsync(existing, Audit(AuditAction.Updated, nameof(Category), existing.Id, new { existing.Name, existing.Description, existing.IsActive }), cancellationToken);
        return MapCategory(existing);
    }

    private async Task<ProductItem> SaveProductAsync(Guid? id, SaveProductRequest request, CancellationToken cancellationToken)
    {
        var name = Required(request.Name, "O nome do produto é obrigatório.", 160);
        var sku = Required(request.Sku, "O SKU é obrigatório.", 80).ToUpperInvariant();
        if (request.SalePrice <= 0) throw new RequestValidationException("O preço de venda deve ser maior que zero.");
        if (request.CostPrice < 0) throw new RequestValidationException("O custo não pode ser negativo.");
        if (request.MinimumStock < 0) throw new RequestValidationException("O estoque mínimo não pode ser negativo.");
        await using var store = await StoreAsync(cancellationToken);
        if (!await store.CategoryIsActiveAsync(request.CategoryId, cancellationToken)) throw new RequestValidationException("Selecione uma categoria ativa.");
        if (await store.ProductSkuExistsAsync(sku, id, cancellationToken)) throw new ResourceConflictException("Já existe um produto com este SKU.");
        var barcode = Clean(request.Barcode);
        if (barcode is not null && await store.ProductBarcodeExistsAsync(barcode, id, cancellationToken)) throw new ResourceConflictException("Já existe um produto com este código de barras.");

        var now = timeProvider.GetUtcNow();
        if (id is null)
        {
            var product = new Product { Id = Guid.NewGuid(), CategoryId = request.CategoryId, Name = name, Sku = sku, Barcode = barcode, SalePrice = request.SalePrice, CostPrice = request.CostPrice, MinimumStock = request.MinimumStock, IsActive = request.IsActive, CreatedAt = now, UpdatedAt = now };
            product.Inventory = InventoryEntity.Create(product.Id, now);
            product.Inventory.Product = product;
            await store.AddProductAsync(product, Audit(AuditAction.Created, nameof(Product), product.Id, SafeProduct(product)), cancellationToken);
            return MapProduct(product);
        }

        var existing = await store.GetProductAsync(id.Value, cancellationToken) ?? throw new ResourceNotFoundException("Produto não encontrado.");
        existing.CategoryId = request.CategoryId;
        existing.Name = name;
        existing.Sku = sku;
        existing.Barcode = barcode;
        existing.SalePrice = request.SalePrice;
        existing.CostPrice = request.CostPrice;
        existing.MinimumStock = request.MinimumStock;
        existing.IsActive = request.IsActive;
        existing.UpdatedAt = now;
        byte[]? expected = null;
        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try { expected = Convert.FromBase64String(request.RowVersion); }
            catch (FormatException) { throw new RequestValidationException("Versão do produto inválida."); }
        }
        await store.SaveProductAsync(existing, expected, Audit(AuditAction.Updated, nameof(Product), existing.Id, SafeProduct(existing)), cancellationToken);
        return MapProduct(existing);
    }

    private async Task<IManagementStore> StoreAsync(CancellationToken cancellationToken)
    {
        if (currentUser.TenantId is not { } tenantId) throw new ResourceNotFoundException("Tenant autenticado não encontrado.");
        var tenant = await tenantResolver.ResolveByTenantIdAsync(tenantId, cancellationToken) ?? throw new ResourceNotFoundException("Tenant autenticado não encontrado.");
        return storeFactory.Create(tenant);
    }

    private AuditLog Audit(AuditAction action, string entity, object id, object details) => new()
    {
        UserId = currentUser.UserId,
        Action = action,
        EntityName = entity,
        EntityId = id.ToString(),
        AfterData = JsonSerializer.Serialize(details),
        IpAddress = currentUser.IpAddress,
        CorrelationId = currentUser.CorrelationId,
        OccurredAt = timeProvider.GetUtcNow()
    };

    private static void ValidateUser(SaveUserRequest request, bool passwordRequired)
    {
        Required(request.Name, "O nome do usuário é obrigatório.", 160);
        var email = Required(request.Email, "O e-mail é obrigatório.", 254);
        if (!email.Contains('@', StringComparison.Ordinal)) throw new RequestValidationException("Informe um e-mail válido.");
        if (passwordRequired || !string.IsNullOrWhiteSpace(request.Password)) EnsurePassword(request.Password);
    }

    private static void EnsurePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12) throw new RequestValidationException("A senha deve ter pelo menos 12 caracteres.");
    }

    private static string Required(string? value, string error, int maxLength)
    {
        var clean = value?.Trim();
        if (string.IsNullOrWhiteSpace(clean)) throw new RequestValidationException(error);
        if (clean.Length > maxLength) throw new RequestValidationException($"{error} Limite de {maxLength} caracteres.");
        return clean;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Guid[] Distinct(IReadOnlyList<Guid>? ids) => (ids ?? []).Distinct().ToArray();
    private static object SafeProduct(Product product) => new { product.Name, product.Sku, product.Barcode, product.CategoryId, product.SalePrice, product.CostPrice, product.MinimumStock, product.IsActive };
    private static UserItem MapUser(User user) => new(user.Id, user.Name, user.Email, user.IsActive, user.CreatedAt, user.UserRoles.Select(x => new LookupItem(x.RoleId, x.Role.Name)).OrderBy(x => x.Name).ToArray());
    private static RoleItem MapRole(Role role) => new(role.Id, role.Name, role.Description, role.IsSystem, role.CreatedAt, role.RolePermissions.Select(x => x.Permission.Code).Order().ToArray());
    private static CategoryItem MapCategory(Category category) => new(category.Id, category.Name, category.Description, category.IsActive, category.CreatedAt, category.UpdatedAt);
    private static ProductItem MapProduct(Product product) => new(product.Id, product.CategoryId, product.Category?.Name ?? string.Empty, product.Name, product.Sku, product.Barcode, product.SalePrice, product.CostPrice, product.MinimumStock, product.IsActive, product.CreatedAt, product.UpdatedAt, Convert.ToBase64String(product.RowVersion));
}
