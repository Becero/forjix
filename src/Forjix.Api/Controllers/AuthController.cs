using System.ComponentModel.DataAnnotations;
using Forjix.Api.Authentication;
using Forjix.Application.Features.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Forjix.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("authentication")]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    IRefreshTokenCookieManager refreshTokenCookieManager) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var session = await authenticationService.LoginAsync(
            new LoginCommand(request.TenantSlug, request.Email, request.Password),
            HttpContext.TraceIdentifier,
            cancellationToken);

        if (session is null)
        {
            return UnauthorizedProblem();
        }

        WriteRefreshCookie(session);
        return Ok(AuthenticationResponse.From(session));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthenticationResponse>> Refresh(CancellationToken cancellationToken)
    {
        var cookie = refreshTokenCookieManager.Read(HttpContext);
        if (cookie is null)
        {
            return UnauthorizedProblem();
        }

        var session = await authenticationService.RefreshAsync(
            new RefreshCommand(cookie.TenantId, cookie.Token),
            HttpContext.TraceIdentifier,
            cancellationToken);

        if (session is null)
        {
            refreshTokenCookieManager.Delete(HttpContext);
            return UnauthorizedProblem();
        }

        WriteRefreshCookie(session);
        return Ok(AuthenticationResponse.From(session));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var cookie = refreshTokenCookieManager.Read(HttpContext);
        if (cookie is not null)
        {
            await authenticationService.LogoutAsync(
                new LogoutCommand(cookie.TenantId, cookie.Token),
                HttpContext.TraceIdentifier,
                cancellationToken);
        }

        refreshTokenCookieManager.Delete(HttpContext);
        return NoContent();
    }

    private ObjectResult UnauthorizedProblem() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Não foi possível autenticar.",
        detail: "Credenciais inválidas.");

    private void WriteRefreshCookie(AuthenticatedSession session) => refreshTokenCookieManager.Write(
        HttpContext,
        new RefreshCookiePayload(session.Context.Tenant.Id, session.RefreshToken),
        session.RefreshTokenExpiresAt);
}

public sealed record LoginRequest(
    [Required, StringLength(80, MinimumLength = 2)] string TenantSlug,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(200, MinimumLength = 8)] string Password);

public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    SessionContext Context)
{
    public static AuthenticationResponse From(AuthenticatedSession session) =>
        new(session.AccessToken, session.AccessTokenExpiresAt, session.Context);
}
