using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Persistence;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Inovait.IntegrationTests.Api;

[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P0")]
public sealed class HealthEndpointsTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    public async Task Ready_WithDatabaseAvailable_ReturnsOkWithReadyStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/health/ready", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("ready", body!.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Live_AlwaysReturnsOkWithLiveStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/health/live", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("live", body!.RootElement.GetProperty("status").GetString());
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitApiHealth_{Guid.NewGuid():N}",
        }.ConnectionString;
        _provider = new ServiceCollection().AddInovaitInfrastructure(connectionString).BuildServiceProvider(true);
        _scope = _provider.CreateAsyncScope();
        _context = _scope.ServiceProvider.GetRequiredService<InovaitDbContext>();
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:InovaitDatabase", connectionString));
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _context.Database.EnsureDeletedAsync();
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}

public sealed class HealthEndpointsNoDatabaseTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointsNoDatabaseTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ready_WithoutDatabaseConfigured_ReturnsServiceUnavailableWithDegradedStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/health/ready", cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("degraded", body!.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Live_WithoutDatabaseConfigured_ReturnsOkWithLiveStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/health/live", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("live", body!.RootElement.GetProperty("status").GetString());
    }
}
