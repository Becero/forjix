using Forjix.Domain.Entities.Sales;

namespace Forjix.Domain.Entities.Customers;

public sealed class Customer
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Document { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<Sale> Sales { get; set; } = [];
}
