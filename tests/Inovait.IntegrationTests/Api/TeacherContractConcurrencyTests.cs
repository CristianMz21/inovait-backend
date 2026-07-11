using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inovait.Api.Contracts;
using Inovait.Core.Domain.People;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Persistence;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inovait.IntegrationTests.Api;

[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P0")]
public sealed class TeacherContractConcurrencyTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    public async Task CreateTeacherContracts_ConcurrentOverlappingRequests_CommitExactlyOneContract()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacherId = await AddTeacherAsync(cancellationToken);

        var responses = await Task.WhenAll(
            _client.PostAsJsonAsync(
                $"/api/teachers/{teacherId}/contracts",
                new { schoolIds = new[] { 1 }, startDate = "2030-01-01", endDate = "2030-06-30" },
                cancellationToken),
            _client.PostAsJsonAsync(
                $"/api/teachers/{teacherId}/contracts",
                new { schoolIds = new[] { 1 }, startDate = "2030-03-01", endDate = "2030-09-30" },
                cancellationToken));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        var statusCodes = new[] { firstResponse.StatusCode, secondResponse.StatusCode };
        Assert.Single(statusCodes, code => code == HttpStatusCode.Created);
        Assert.Single(statusCodes, code => code == HttpStatusCode.Conflict);

        var winner = firstResponse.StatusCode == HttpStatusCode.Created ? firstResponse : secondResponse;
        var loser = firstResponse.StatusCode == HttpStatusCode.Conflict ? firstResponse : secondResponse;
        var conflictBody = await loser.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain("Microsoft.Data.SqlClient", conflictBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT ", conflictBody, StringComparison.Ordinal);
        using var conflictProblem = JsonDocument.Parse(conflictBody);
        Assert.Equal("teacher_contract_conflict", conflictProblem.RootElement.GetProperty("code").GetString());

        var contract = Assert.Single(await _context.TeacherContracts.AsNoTracking().ToListAsync(cancellationToken));
        Assert.Equal((teacherId, 1), (contract.TeacherPersonId, contract.SchoolId));
        var created = await winner.Content.ReadFromJsonAsync<List<TeacherContractResponse>>(JsonOptions, cancellationToken);
        var createdContract = Assert.Single(created!);
        Assert.Equal((contract.StartDate, contract.EndDate), (createdContract.StartDate, createdContract.EndDate));
    }

    [Fact]
    public async Task CreateTeacherContracts_ConcurrencyConflict_Returns409ProblemDetailsWithoutSqlDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacherId = await AddTeacherAsync(cancellationToken);

        static object BuildRequest() => new { schoolIds = new[] { 1 }, startDate = "2031-01-01", endDate = "2031-12-31" };
        var responses = await Task.WhenAll(
            _client.PostAsJsonAsync($"/api/teachers/{teacherId}/contracts", BuildRequest(), cancellationToken),
            _client.PostAsJsonAsync($"/api/teachers/{teacherId}/contracts", BuildRequest(), cancellationToken));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        var statusCodes = new[] { firstResponse.StatusCode, secondResponse.StatusCode };
        Assert.Single(statusCodes, code => code == HttpStatusCode.Created);
        Assert.Single(statusCodes, code => code == HttpStatusCode.Conflict);

        var loser = firstResponse.StatusCode == HttpStatusCode.Conflict ? firstResponse : secondResponse;
        Assert.Equal("application/problem+json", loser.Content.Headers.ContentType?.MediaType);
        var body = await loser.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain("Microsoft.Data.SqlClient", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT ", body, StringComparison.Ordinal);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("teacher_contract_conflict", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "https://inovait.local/problems/teacher-contract-conflict",
            problem.RootElement.GetProperty("type").GetString());
        Assert.True(problem.RootElement.TryGetProperty("title", out _));

        Assert.Equal(1, await _context.TeacherContracts.CountAsync(cancellationToken));
    }

    private async Task<int> AddTeacherAsync(CancellationToken cancellationToken)
    {
        var person = new Person(1, "T-CONCURRENCY", "Test", "Teacher", new(1990, 1, 1));
        _context.Add(person);
        await _context.SaveChangesAsync(cancellationToken);
        _context.Add(new Teacher(person.Id));
        await _context.SaveChangesAsync(cancellationToken);
        return person.Id;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitApiTeacherContractConcurrency_{Guid.NewGuid():N}",
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
