using Forjix.Application.Abstractions.Authorization;
using Forjix.Application.Features.Authentication;
using Forjix.Application.Features.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Forjix.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        return services;
    }
}
