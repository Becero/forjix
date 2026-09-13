using Forjix.Domain.Entities.Inventory;
using Forjix.Domain.Enums;

namespace Forjix.Domain.Tests;

public sealed class InventoryTests
{
    [Theory]
    [InlineData(InventoryMovementType.StockEntry, 10, 10)]
    [InlineData(InventoryMovementType.StockExit, 3, -3)]
    [InlineData(InventoryMovementType.PositiveAdjustment, 5, 5)]
    [InlineData(InventoryMovementType.NegativeAdjustment, 2, -2)]
    public void MovementTypeDeterminesDirection(InventoryMovementType type, decimal quantity, decimal expected)
    {
        var inventory = Inventory.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var change = inventory.ApplyMovement(type, quantity, true, DateTimeOffset.UtcNow);
        Assert.Equal(expected, inventory.Quantity);
        Assert.Equal(0, change.PreviousQuantity);
        Assert.Equal(expected, change.NewQuantity);
    }

    [Fact]
    public void SequentialMovementsKeepTheCorrectBalance()
    {
        var inventory = Inventory.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
        inventory.ApplyMovement(InventoryMovementType.StockEntry, 20, false, DateTimeOffset.UtcNow);
        inventory.ApplyMovement(InventoryMovementType.StockExit, 3, false, DateTimeOffset.UtcNow);
        inventory.ApplyMovement(InventoryMovementType.NegativeAdjustment, 2, false, DateTimeOffset.UtcNow);
        inventory.ApplyMovement(InventoryMovementType.PositiveAdjustment, 5, false, DateTimeOffset.UtcNow);
        Assert.Equal(20, inventory.Quantity);
    }

    [Fact]
    public void NegativeStockIsRejectedWithoutChangingBalance()
    {
        var inventory = Inventory.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => inventory.ApplyMovement(InventoryMovementType.StockExit, 1, false, DateTimeOffset.UtcNow));
        Assert.Equal(0, inventory.Quantity);
    }

    [Fact]
    public void NegativeStockCanBeEnabled()
    {
        var inventory = Inventory.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
        inventory.ApplyMovement(InventoryMovementType.StockExit, 1, true, DateTimeOffset.UtcNow);
        Assert.Equal(-1, inventory.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveQuantityIsRejected(decimal quantity)
    {
        var inventory = Inventory.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.ApplyMovement(InventoryMovementType.StockEntry, quantity, false, DateTimeOffset.UtcNow));
    }
}
