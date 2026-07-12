using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inovait.Api.Contracts;
using Inovait.Core.Domain.Catalogs;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Persistence;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inovait.IntegrationTests.Api;

[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P1")]
public sealed class ListSubjectsTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-LIST-SUBJECTS")]
    public async Task ListSubjects_ReturnsCanonicalDtoOrderedByNameThenCodeThenId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Insertion order (id order) and code order are both deliberately misaligned with name order,
        // so a passing assertion proves the primary sort key is name, not id or code.
        var history = new Subject("Z-HIS", "History");
        _context.Add(history);
        await _context.SaveChangesAsync(cancellationToken);
        var science = new Subject("M-SCI", "Science");
        _context.Add(science);
        await _context.SaveChangesAsync(cancellationToken);
        var art = new Subject("A-ART", "Art");
        _context.Add(art);
        await _context.SaveChangesAsync(cancellationToken);

        using var rawResponse = await _client.GetAsync("/api/subjects", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, rawResponse.StatusCode);
        Assert.Equal("application/json", rawResponse.Content.Headers.ContentType?.MediaType);
        var rawJson = await rawResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal(
            new[] { "id", "code", "name" },
            rawJson!.RootElement[0].EnumerateObject().Select(property => property.Name));

        var subjects = await GetAsync<List<SubjectSummary>>("/api/subjects", cancellationToken);
        Assert.Equal(
            [(art.Id, "A-ART", "Art"), (history.Id, "Z-HIS", "History"), (science.Id, "M-SCI", "Science")],
            subjects.Select(subject => (subject.Id, subject.Code, subject.Name)));
    }

    [Fact]
    [Trait("Evidence", "IT-LIST-SUBJECTS")]
    public async Task ListSubjects_ReturnsEmptyArrayWhenNoneSeeded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/api/subjects", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("[]", (await response.Content.ReadAsStringAsync(cancellationToken)).Trim());
    }

    private async Task<T> GetAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(requestUri, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return payload!;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitApiSubjects_{Guid.NewGuid():N}",
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
