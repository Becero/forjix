namespace Forjix.Domain.Enums;

public enum InventoryMovementType
{
    StockEntry = 0,
    StockExit = 1,
    PositiveAdjustment = 2,
    NegativeAdjustment = 3,
    Sale = 4,
    SaleCancellation = 5,
    Purchase = 6
}
