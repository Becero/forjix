namespace Forjix.Application.Abstractions.Authorization;

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(
        Guid tenantId,
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default);
}
