using Forjix.Application.Abstractions.Authorization;
using Forjix.Application.Features.Authentication;
using Forjix.Application.Features.Customers;
using Forjix.Application.Features.Authorization;
using Forjix.Application.Features.Inventory;
using Forjix.Application.Features.Management;
using Forjix.Application.Features.Purchases;
using Forjix.Application.Features.Sales;
using Microsoft.Extensions.DependencyInjection;

namespace Forjix.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddScoped<IManagementService, ManagementService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        return services;
    }
}
