namespace Forjix.Application.Abstractions.Authentication;

public interface IRefreshTokenGenerator
{
    GeneratedRefreshToken Generate();
    string Hash(string token);
}

public sealed record GeneratedRefreshToken(string Token, string Hash);
