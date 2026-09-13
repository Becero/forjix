using System.Security.Claims;
using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Forjix.Infrastructure.Authentication;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId => ReadGuid(ForjixClaimNames.UserId);
    public Guid? TenantId => ReadGuid(ForjixClaimNames.TenantId);
    public string? TenantSlug => Principal?.FindFirstValue(ForjixClaimNames.TenantSlug);
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(Principal?.FindFirstValue(claimType), out var value) ? value : null;
}
