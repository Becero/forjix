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

        var configuredSecret = configuration[secretReference];
        if (!string.IsNullOrWhiteSpace(configuredSecret))
        {
            return Task.FromResult<string?>(configuredSecret.Trim().Trim('"'));
        }

        var environmentKey = secretReference.Replace(":", "__", StringComparison.Ordinal);
        var environmentSecret = Environment.GetEnvironmentVariable(environmentKey);
        if (!string.IsNullOrWhiteSpace(environmentSecret))
        {
            return Task.FromResult<string?>(environmentSecret.Trim().Trim('"'));
        }

        var normalizedEnvironmentKey = environmentKey.Replace("-", "_", StringComparison.Ordinal);
        environmentSecret = Environment.GetEnvironmentVariable(normalizedEnvironmentKey);

        return Task.FromResult<string?>(
            string.IsNullOrWhiteSpace(environmentSecret)
                ? null
                : environmentSecret.Trim().Trim('"'));
    }
}
