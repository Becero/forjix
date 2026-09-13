namespace Forjix.Domain.Entities.Identity;

public sealed class Permission
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Module { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

