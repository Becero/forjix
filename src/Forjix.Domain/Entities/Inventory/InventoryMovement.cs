using Forjix.Domain.Entities.Catalog;
using Forjix.Domain.Entities.Identity;
using Forjix.Domain.Enums;

namespace Forjix.Domain.Entities.Inventory;

public sealed class InventoryMovement
{
    public long Id { get; set; }
    public Guid InventoryId { get; set; }
    public Guid ProductId { get; set; }
    public InventoryMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal PreviousQuantity { get; set; }
    public decimal NewQuantity { get; set; }
    public string? Reason { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Inventory Inventory { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public User User { get; set; } = null!;
}
