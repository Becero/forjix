namespace Forjix.Application.Features.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticatedSession?> LoginAsync(LoginCommand command, string correlationId, CancellationToken cancellationToken = default);
    Task<AuthenticatedSession?> RefreshAsync(RefreshCommand command, string correlationId, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(LogoutCommand command, string correlationId, CancellationToken cancellationToken = default);
    Task<SessionContext?> GetSessionContextAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}
