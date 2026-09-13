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
    StockMovementCreated = 8
}
