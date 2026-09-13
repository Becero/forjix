using Forjix.Application.Abstractions.Security;
using Microsoft.Extensions.Configuration;

namespace Forjix.Infrastructure.Secrets;

internal sealed class ConfigurationSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public Task<string?> GetSecretAsync(
        string secretReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(secretReference);
        return Task.FromResult(configuration[secretReference]);
    }
}

