using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Forjix.IntegrationTests;

public sealed class HealthEndpointTests(ForjixWebApplicationFactory factory)
    : IClassFixture<ForjixWebApplicationFactory>
{
    [Fact]
    public async Task LiveEndpointReturnsSuccess()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync(new Uri("/api/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingApiEndpointIsNotInterceptedBySpaFallback()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync(new Uri("/api/not-a-real-endpoint", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
