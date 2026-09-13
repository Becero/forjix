using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Forjix.IntegrationTests;

public sealed class ForjixWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:ForjixMaster"] = "Server=test;Database=ForjixMaster;User Id=test;Password=test;TrustServerCertificate=True",
                ["Jwt:Issuer"] = "Forjix.Tests",
                ["Jwt:Audience"] = "Forjix.Tests",
                ["Jwt:SigningKey"] = "test-only-signing-key-with-at-least-32-bytes"
            }));
    }
}
