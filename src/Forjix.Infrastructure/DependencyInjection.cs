using System.Text;
using Forjix.Application.Abstractions.Authentication;
using Forjix.Application.Abstractions.Analytics;
using Forjix.Application.Abstractions.Cash;
using Forjix.Application.Abstractions.Customers;
using Forjix.Application.Abstractions.Authorization;
using Forjix.Application.Abstractions.Identity;
using Forjix.Application.Abstractions.Inventory;
using Forjix.Application.Abstractions.Management;
using Forjix.Application.Abstractions.Purchases;
using Forjix.Application.Abstractions.Security;
using Forjix.Application.Abstractions.Settings;
using Forjix.Application.Abstractions.Sales;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Domain.Entities.Identity;
using Forjix.Infrastructure.Authentication;
using Forjix.Infrastructure.Analytics;
using Forjix.Infrastructure.Cash;
using Forjix.Infrastructure.Customers;
using Forjix.Infrastructure.Authorization;
using Forjix.Infrastructure.Inventory;
using Forjix.Infrastructure.Management;
using Forjix.Infrastructure.Purchases;
using Forjix.Infrastructure.Persistence.Master;
using Forjix.Infrastructure.Persistence.Tenant;
using Forjix.Infrastructure.Secrets;
using Forjix.Infrastructure.Settings;
using Forjix.Infrastructure.Sales;
using Forjix.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Forjix.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var masterConnectionString = configuration.GetConnectionString("ForjixMaster");
        if (string.IsNullOrWhiteSpace(masterConnectionString))
        {
            throw new InvalidOperationException("Configure ConnectionStrings:ForjixMaster outside source control.");
        }

        services.AddDbContext<ForjixMasterDbContext>(options =>
        {
            options.UseSqlServer(masterConnectionString);
        });

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT audience is required.")
            .Validate(options => Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) >= 32,
                "JWT signing key must contain at least 32 bytes.")
            .Validate(options => options.AccessTokenMinutes is >= 1 and <= 60,
                "JWT access token lifetime must be between 1 and 60 minutes.")
            .Validate(options => options.ClockSkewSeconds is >= 0 and <= 300,
                "JWT clock skew must be between 0 and 300 seconds.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? throw new OptionsValidationException(
                        JwtOptions.SectionName,
                        typeof(JwtOptions),
                        ["JWT configuration is required."]);

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });
        services.AddAuthorization();

        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<ITenantIdentityStoreFactory, TenantIdentityStoreFactory>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
        services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();
        services.AddScoped<IManagementStoreFactory, ManagementStoreFactory>();
        services.AddScoped<IInventoryStoreFactory, InventoryStoreFactory>();
        services.AddScoped<ISalesStoreFactory, SalesStoreFactory>();
        services.AddScoped<ICustomerStoreFactory, CustomerStoreFactory>();
        services.AddScoped<IPurchaseStoreFactory, PurchaseStoreFactory>();
        services.AddScoped<ICashStoreFactory, CashStoreFactory>();
        services.AddScoped<IAnalyticsStoreFactory, AnalyticsStoreFactory>();
        services.AddScoped<ISettingsStore, SettingsStore>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
