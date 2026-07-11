using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Inovait.IntegrationTests;

public sealed class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Root_ReturnsServiceReadinessContract()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var rootResponse = await _client.GetAsync("/", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, rootResponse.StatusCode);
        Assert.Equal("application/json", rootResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "{\"service\":\"Inovait API\",\"status\":\"ready\"}",
            await rootResponse.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task Health_ReturnsHealthyContract()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var healthResponse = await _client.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Equal("application/json", healthResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "{\"status\":\"ok\"}",
            await healthResponse.Content.ReadAsStringAsync(cancellationToken));
    }
}
