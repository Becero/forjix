using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Forjix.Api.Authentication;

public sealed record RefreshCookiePayload(Guid TenantId, string Token);

public interface IRefreshTokenCookieManager
{
    void Write(HttpContext context, RefreshCookiePayload payload, DateTimeOffset expiresAt);
    RefreshCookiePayload? Read(HttpContext context);
    void Delete(HttpContext context);
}

internal sealed class RefreshTokenCookieManager(
    IDataProtectionProvider dataProtectionProvider,
    IWebHostEnvironment environment) : IRefreshTokenCookieManager
{
    private const string DevelopmentCookieName = "forjix.refresh";
    private const string ProductionCookieName = "__Secure-forjix-refresh";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Forjix.RefreshTokenCookie.v1");
    private string CookieName => environment.IsDevelopment() ? DevelopmentCookieName : ProductionCookieName;

    public void Write(HttpContext context, RefreshCookiePayload payload, DateTimeOffset expiresAt)
    {
        var protectedValue = _protector.Protect(JsonSerializer.Serialize(payload));
        context.Response.Cookies.Append(CookieName, protectedValue, Options(context, expiresAt));
    }

    public RefreshCookiePayload? Read(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RefreshCookiePayload>(_protector.Unprotect(value));
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or JsonException)
        {
            return null;
        }
    }

    public void Delete(HttpContext context) => context.Response.Cookies.Delete(CookieName, Options(context, null));

    private CookieOptions Options(HttpContext context, DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expiresAt,
        IsEssential = true
    };
}
