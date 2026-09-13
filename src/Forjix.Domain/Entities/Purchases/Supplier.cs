namespace Forjix.Domain.Entities.Purchases;

public sealed class Supplier { public Guid Id { get; set; } public required string Name { get; set; } public string? Document { get; set; } public string? Email { get; set; } public string? Phone { get; set; } public string? ContactName { get; set; } public string? Notes { get; set; } public bool IsActive { get; set; } = true; public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset UpdatedAt { get; set; } public ICollection<Purchase> Purchases { get; set; } = []; }
