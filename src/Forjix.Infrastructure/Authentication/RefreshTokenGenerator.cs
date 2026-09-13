using System.Security.Cryptography;
using System.Text;
using Forjix.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.WebUtilities;

namespace Forjix.Infrastructure.Authentication;

internal sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public GeneratedRefreshToken Generate()
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        return new GeneratedRefreshToken(token, Hash(token));
    }

    public string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
