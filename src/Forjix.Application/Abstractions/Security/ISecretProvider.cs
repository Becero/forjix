namespace Forjix.Application.Abstractions.Security;

public interface ISecretProvider
{
    Task<string?> GetSecretAsync(
        string secretReference,
        CancellationToken cancellationToken = default);
}

