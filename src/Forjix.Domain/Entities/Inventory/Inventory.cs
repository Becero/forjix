using Forjix.Domain.Entities.Catalog;
using Forjix.Domain.Enums;

namespace Forjix.Domain.Entities.Inventory;

public sealed class Inventory
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] RowVersion { get; set; } = [];

    public Product Product { get; set; } = null!;
    public ICollection<InventoryMovement> Movements { get; set; } = [];

    public static Inventory Create(Guid productId, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = productId,
        Quantity = 0,
        UpdatedAt = createdAt
    };

    public InventoryChange ApplyMovement(
        InventoryMovementType type,
        decimal quantity,
        bool allowNegativeStock,
        DateTimeOffset occurredAt)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        var delta = type switch
        {
            InventoryMovementType.StockEntry or InventoryMovementType.PositiveAdjustment => quantity,
            InventoryMovementType.StockExit or InventoryMovementType.NegativeAdjustment => -quantity,
            _ => throw new ArgumentOutOfRangeException(nameof(type), "Unsupported inventory movement type.")
        };
        var previous = Quantity;
        var next = previous + delta;
        if (!allowNegativeStock && next < 0) throw new InvalidOperationException("Negative stock is not allowed.");
        Quantity = next;
        UpdatedAt = occurredAt;
        return new InventoryChange(previous, next, delta);
    }
}

public readonly record struct InventoryChange(decimal PreviousQuantity, decimal NewQuantity, decimal Delta);
