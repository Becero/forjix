namespace Forjix.Domain.Enums;

public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
    LoginSucceeded = 3,
    LoginFailed = 4,
    Logout = 5,
    RefreshTokenRevoked = 6,
    PermissionChanged = 7,
    StockMovementCreated = 8,
    SaleCreated = 9,
    SaleCancelled = 10,
    CustomerCreated = 11,
    CustomerUpdated = 12,
    SupplierCreated = 13,
    SupplierUpdated = 14,
    PurchaseCreated = 15,
    PurchaseReceived = 16,
    PurchaseCancelled = 17,
    CashOpened = 18,
    CashClosed = 19,
    CashWithdrawal = 20,
    CashSupply = 21
}
