using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Forjix.IntegrationTests;

public sealed class ForjixWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var configuredMaster = Environment.GetEnvironmentVariable("ConnectionStrings__ForjixMaster");
        var configuredKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:ForjixMaster"] = configuredMaster ?? "Server=test;Database=ForjixMaster;User Id=test;Password=test;TrustServerCertificate=True",
                ["Jwt:Issuer"] = "Forjix.Tests",
                ["Jwt:Audience"] = "Forjix.Tests",
                ["Jwt:SigningKey"] = configuredKey ?? "test-only-signing-key-with-at-least-32-bytes",
                ["RateLimiting:AuthenticationPermitLimit"] = "100"
            }));
    }
}
