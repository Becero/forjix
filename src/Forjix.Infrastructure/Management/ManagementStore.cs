using Forjix.Application.Abstractions.Management;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Application.Common;
using Forjix.Application.Features.Management;
using Forjix.Domain.Entities.Audit;
using Forjix.Domain.Entities.Catalog;
using Forjix.Domain.Entities.Identity;
using Forjix.Domain.Enums;
using Forjix.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Management;

internal sealed class ManagementStoreFactory(ITenantDbContextFactory contextFactory) : IManagementStoreFactory
{
    public IManagementStore Create(ResolvedTenantDatabase tenant) => new ManagementStore(contextFactory.Create(tenant));
}

internal sealed class ManagementStore(TenantDbContext db) : IManagementStore
{
    public Task<List<User>> GetUsersAsync(CancellationToken cancellationToken) => Users().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    public Task<User?> GetUserAsync(Guid id, CancellationToken cancellationToken) => Users().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? exceptId, CancellationToken cancellationToken) => db.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail && (!exceptId.HasValue || x.Id != exceptId), cancellationToken);
    public async Task<bool> RolesExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => ids.Count == 0 || await db.Roles.CountAsync(x => ids.Contains(x.Id), cancellationToken) == ids.Count;

    public async Task AddUserAsync(User user, IReadOnlyCollection<Guid> roleIds, AuditLog audit, CancellationToken cancellationToken)
    {
        db.Users.Add(user);
        await SetUserRolesAsync(user, roleIds, cancellationToken);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveUserAsync(User user, IReadOnlyCollection<Guid> roleIds, AuditLog audit, CancellationToken cancellationToken)
    {
        await SetUserRolesAsync(user, roleIds, cancellationToken);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Role>> GetRolesAsync(CancellationToken cancellationToken) => Roles().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    public Task<Role?> GetRoleAsync(Guid id, CancellationToken cancellationToken) => Roles().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<List<Permission>> GetPermissionsAsync(CancellationToken cancellationToken) => db.Permissions.AsNoTracking().OrderBy(x => x.Module).ThenBy(x => x.Code).ToListAsync(cancellationToken);
    public Task<bool> RoleNameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) => db.Roles.AnyAsync(x => x.NormalizedName == normalizedName && (!exceptId.HasValue || x.Id != exceptId), cancellationToken);
    public async Task<bool> PermissionsExistAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken) => codes.Count == 0 || await db.Permissions.CountAsync(x => codes.Contains(x.Code), cancellationToken) == codes.Count;

    public async Task AddRoleAsync(Role role, IReadOnlyCollection<string> permissions, AuditLog audit, CancellationToken cancellationToken)
    {
        db.Roles.Add(role);
        await SetRolePermissionsAsync(role, permissions, audit.OccurredAt, cancellationToken);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveRoleAsync(Role role, IReadOnlyCollection<string> permissions, AuditLog audit, CancellationToken cancellationToken)
    {
        await SetRolePermissionsAsync(role, permissions, audit.OccurredAt, cancellationToken);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<AuditItem>> GetAuditAsync(Guid? userId, string? action, string? entity, DateTimeOffset? from, DateTimeOffset? toDate, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (userId.HasValue) query = query.Where(x => x.UserId == userId);
        if (Enum.TryParse<AuditAction>(action, true, out var parsedAction)) query = query.Where(x => x.Action == parsedAction);
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(x => x.EntityName == entity);
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from);
        if (toDate.HasValue) query = query.Where(x => x.OccurredAt <= toDate);

        return await (from log in query
                      join user in db.Users.AsNoTracking() on log.UserId equals user.Id into users
                      from user in users.DefaultIfEmpty()
                      orderby log.OccurredAt descending
                      select new AuditItem(log.Id, log.OccurredAt, user == null ? null : user.Name, log.Action.ToString(), log.EntityName, log.EntityId, log.AfterData)).Take(500).ToListAsync(cancellationToken);
    }

    public Task<List<Category>> GetCategoriesAsync(bool includeInactive, CancellationToken cancellationToken) => db.Categories.AsNoTracking().Where(x => includeInactive || x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
    public Task<Category?> GetCategoryAsync(Guid id, CancellationToken cancellationToken) => db.Categories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> CategoryNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken) => db.Categories.AnyAsync(x => x.Name == name && (!exceptId.HasValue || x.Id != exceptId), cancellationToken);
    public Task AddCategoryAsync(Category category, AuditLog audit, CancellationToken cancellationToken) => AddAndAuditAsync(category, audit, cancellationToken);
    public Task SaveCategoryAsync(Category category, AuditLog audit, CancellationToken cancellationToken) => AuditAndSaveAsync(audit, cancellationToken);

    public Task<List<Product>> GetProductsAsync(string? search, Guid? categoryId, bool? isActive, CancellationToken cancellationToken)
    {
        var query = db.Products.AsNoTracking().Include(x => x.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.Sku.Contains(search) || (x.Barcode != null && x.Barcode.Contains(search)));
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive);
        return query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken) => db.Products.Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> ProductSkuExistsAsync(string sku, Guid? exceptId, CancellationToken cancellationToken) => db.Products.AnyAsync(x => x.Sku == sku && (!exceptId.HasValue || x.Id != exceptId), cancellationToken);
    public Task<bool> ProductBarcodeExistsAsync(string barcode, Guid? exceptId, CancellationToken cancellationToken) => db.Products.AnyAsync(x => x.Barcode == barcode && (!exceptId.HasValue || x.Id != exceptId), cancellationToken);
    public Task<bool> CategoryIsActiveAsync(Guid id, CancellationToken cancellationToken) => db.Categories.AnyAsync(x => x.Id == id && x.IsActive, cancellationToken);

    public async Task AddProductAsync(Product product, AuditLog audit, CancellationToken cancellationToken)
    {
        product.Category = await db.Categories.SingleAsync(x => x.Id == product.CategoryId, cancellationToken);
        await AddAndAuditAsync(product, audit, cancellationToken);
    }

    public async Task SaveProductAsync(Product product, byte[]? expectedRowVersion, AuditLog audit, CancellationToken cancellationToken)
    {
        if (expectedRowVersion is not null) db.Entry(product).Property(x => x.RowVersion).OriginalValue = expectedRowVersion;
        product.Category = await db.Categories.SingleAsync(x => x.Id == product.CategoryId, cancellationToken);
        try { await AuditAndSaveAsync(audit, cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ResourceConflictException("O produto foi alterado por outro usuário. Atualize a página e tente novamente."); }
    }

    public ValueTask DisposeAsync() => db.DisposeAsync();

    private IQueryable<User> Users() => db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role);
    private IQueryable<Role> Roles() => db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission);

    private async Task SetUserRolesAsync(User user, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var roles = await db.Roles.Where(x => roleIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var removed = user.UserRoles.Where(x => !roleIds.Contains(x.RoleId)).ToArray();
        db.UserRoles.RemoveRange(removed);
        foreach (var item in removed) user.UserRoles.Remove(item);
        foreach (var role in roles.Where(role => user.UserRoles.All(x => x.RoleId != role.Id)))
            user.UserRoles.Add(new UserRole { User = user, Role = role, UserId = user.Id, RoleId = role.Id, AssignedAt = DateTimeOffset.UtcNow });
    }

    private async Task SetRolePermissionsAsync(Role role, IReadOnlyCollection<string> codes, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var permissions = await db.Permissions.Where(x => codes.Contains(x.Code)).ToListAsync(cancellationToken);
        var removed = role.RolePermissions.Where(x => !codes.Contains(x.Permission.Code)).ToArray();
        db.RolePermissions.RemoveRange(removed);
        foreach (var item in removed) role.RolePermissions.Remove(item);
        foreach (var permission in permissions.Where(permission => role.RolePermissions.All(x => x.PermissionId != permission.Id)))
            role.RolePermissions.Add(new RolePermission { Role = role, Permission = permission, RoleId = role.Id, PermissionId = permission.Id, GrantedAt = now });
    }

    private async Task AddAndAuditAsync<TEntity>(TEntity entity, AuditLog audit, CancellationToken cancellationToken) where TEntity : class
    {
        db.Add(entity);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task AuditAndSaveAsync(AuditLog audit, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(cancellationToken);
    }
}
