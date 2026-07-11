using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inovait.Api.Contracts;
using Inovait.Core.Domain.Catalogs;
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
public sealed class TeacherContractEndpointsTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-CON-MULTI")]
    public async Task CreateTeacherContracts_IsAtomicAcrossSchoolsAndRejectsConflictsWithoutPartialWrites()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _context.Add(new School("SCH-AUR", "Aurora School", SchoolSector.Private));
        await _context.SaveChangesAsync(cancellationToken);
        var teacherId = await AddTeacherAsync(cancellationToken);

        var request = new { schoolIds = new[] { 1, 2 }, startDate = "2020-01-01", endDate = "2020-12-31" };
        using var createdResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts", request, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<List<TeacherContractResponse>>(JsonOptions, cancellationToken);
        Assert.NotNull(created);
        Assert.Equal(["Aurora School", "North Learning Center"], created!.Select(item => item.School.Name));
        Assert.All(created, item => Assert.Equal(teacherId, item.TeacherId));
        Assert.All(created, item => Assert.Equal("Confirmed", item.PersistedStatus));
        Assert.All(created, item => Assert.Equal("Expired", item.EffectiveStatus));
        Assert.All(created, item => Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), item.EvaluatedAt));

        using var overlapResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts", new { schoolIds = new[] { 1 }, startDate = "2020-06-01", endDate = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, overlapResponse.StatusCode);
        var overlapProblem = await overlapResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("teacher_contract_conflict", overlapProblem!.RootElement.GetProperty("code").GetString());

        using var duplicateResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts", new { schoolIds = new[] { 1, 1 }, startDate = "2021-01-01", endDate = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var duplicateProblem = await duplicateResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("teacher_contract_conflict", duplicateProblem!.RootElement.GetProperty("code").GetString());

        Assert.Equal(2, await _context.TeacherContracts.CountAsync(cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-CON-LIST")]
    public async Task ListTeacherContracts_OrdersByStartDateThenSchoolAndRequiresExistingTeacher()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _context.Add(new School("SCH-AUR", "Aurora School", SchoolSector.Private));
        await _context.SaveChangesAsync(cancellationToken);
        var teacherId = await AddTeacherAsync(cancellationToken);

        using var createdResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts",
            new { schoolIds = new[] { 1, 2 }, startDate = "2020-01-01", endDate = "2020-12-31" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);

        using var listResponse = await _client.GetAsync($"/api/teachers/{teacherId}/contracts", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var contracts = await listResponse.Content.ReadFromJsonAsync<List<TeacherContractResponse>>(JsonOptions, cancellationToken);
        Assert.NotNull(contracts);
        Assert.Equal(["Aurora School", "North Learning Center"], contracts!.Select(item => item.School.Name));

        using var missingTeacherResponse = await _client.GetAsync("/api/teachers/999999/contracts", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingTeacherResponse.StatusCode);
        var problem = await missingTeacherResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("teacher_not_found", problem!.RootElement.GetProperty("code").GetString());
    }

    private async Task<int> AddTeacherAsync(CancellationToken cancellationToken)
    {
        var person = new Person(1, "T-CONTRACT", "Test", "Teacher", new(1990, 1, 1));
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
            InitialCatalog = $"InovaitApiTeacherContract_{Guid.NewGuid():N}",
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
