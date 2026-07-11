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

    [Fact]
    [Trait("Evidence", "IT-CON-DATES")]
    public async Task CreateTeacherContracts_PersistsDateRangeAndOpenEndedContractsAcrossSchools()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _context.Add(new School("SCH-AUR", "Aurora School", SchoolSector.Private));
        await _context.SaveChangesAsync(cancellationToken);
        var teacherId = await AddTeacherAsync(cancellationToken);

        using var rangeResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts",
            new { schoolIds = new[] { 1 }, startDate = "2020-01-01", endDate = "2020-12-31" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, rangeResponse.StatusCode);
        var ranged = await rangeResponse.Content.ReadFromJsonAsync<List<TeacherContractResponse>>(JsonOptions, cancellationToken);
        var rangedContract = Assert.Single(ranged!);
        Assert.Equal(
            (teacherId, 1, "North Learning Center", new DateOnly(2020, 1, 1), (DateOnly?)new DateOnly(2020, 12, 31)),
            (rangedContract.TeacherId, rangedContract.School.Id, rangedContract.School.Name,
                rangedContract.StartDate, rangedContract.EndDate));
        Assert.Equal(("Confirmed", "Expired"), (rangedContract.PersistedStatus, rangedContract.EffectiveStatus));

        using var openResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts",
            new { schoolIds = new[] { 2 }, startDate = "2020-06-01", endDate = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);
        var open = await openResponse.Content.ReadFromJsonAsync<List<TeacherContractResponse>>(JsonOptions, cancellationToken);
        var openContract = Assert.Single(open!);
        Assert.Equal(
            (2, "Aurora School", new DateOnly(2020, 6, 1), (DateOnly?)null),
            (openContract.School.Id, openContract.School.Name, openContract.StartDate, openContract.EndDate));
        Assert.Equal(("Confirmed", "Effective"), (openContract.PersistedStatus, openContract.EffectiveStatus));

        Assert.Equal(2, await _context.TeacherContracts.CountAsync(cancellationToken));
        Assert.Equal(1, await _context.TeacherContracts.CountAsync(
            contract => contract.EndDate == null, cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-CON-DATES")]
    public async Task CreateTeacherContracts_InvalidRangeOrInvalidRequest_IsRejectedWithoutPersisting()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacherId = await AddTeacherAsync(cancellationToken);

        using var invalidRangeResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts",
            new { schoolIds = new[] { 1 }, startDate = "2026-12-31", endDate = "2026-01-01" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidRangeResponse.StatusCode);
        Assert.Equal("application/problem+json", invalidRangeResponse.Content.Headers.ContentType?.MediaType);
        var invalidRangeProblem = await invalidRangeResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal(422, invalidRangeProblem!.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("invalid_date_range", invalidRangeProblem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "https://inovait.local/problems/invalid-date-range",
            invalidRangeProblem.RootElement.GetProperty("type").GetString());

        using var invalidRequestResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts",
            new { schoolIds = Array.Empty<int>(), startDate = (string?)null, endDate = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRequestResponse.StatusCode);
        var invalidRequestProblem = await invalidRequestResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("invalid_request", invalidRequestProblem!.RootElement.GetProperty("code").GetString());
        var errors = invalidRequestProblem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("schoolIds", out _));
        Assert.True(errors.TryGetProperty("startDate", out _));

        Assert.Equal(0, await _context.TeacherContracts.CountAsync(cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-CON-DATES")]
    public async Task CreateTeacherContracts_MissingTeacherOrSchool_ReturnsNotFoundWithoutPersisting()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacherId = await AddTeacherAsync(cancellationToken);

        using var missingTeacherResponse = await _client.PostAsJsonAsync(
            "/api/teachers/999999/contracts",
            new { schoolIds = new[] { 1 }, startDate = "2026-01-01", endDate = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingTeacherResponse.StatusCode);
        var teacherProblem = await missingTeacherResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("teacher_not_found", teacherProblem!.RootElement.GetProperty("code").GetString());

        using var missingSchoolResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts",
            new { schoolIds = new[] { 999999 }, startDate = "2026-01-01", endDate = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingSchoolResponse.StatusCode);
        var schoolProblem = await missingSchoolResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("school_not_found", schoolProblem!.RootElement.GetProperty("code").GetString());

        Assert.Equal(0, await _context.TeacherContracts.CountAsync(cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-CON-DATES")]
    public async Task CreateTeacherContracts_MultiSchoolBatchWithInvalidItem_PersistsNoPartialResults()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _context.Add(new School("SCH-AUR", "Aurora School", SchoolSector.Private));
        await _context.SaveChangesAsync(cancellationToken);
        var teacherId = await AddTeacherAsync(cancellationToken);

        using var baselineResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts",
            new { schoolIds = new[] { 1 }, startDate = "2027-01-01", endDate = "2027-12-31" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, baselineResponse.StatusCode);

        using var missingSchoolBatchResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts",
            new { schoolIds = new[] { 2, 999999 }, startDate = "2028-01-01", endDate = "2028-12-31" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingSchoolBatchResponse.StatusCode);
        var missingSchoolProblem = await missingSchoolBatchResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("school_not_found", missingSchoolProblem!.RootElement.GetProperty("code").GetString());

        using var overlappingBatchResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts",
            new { schoolIds = new[] { 2, 1 }, startDate = "2027-06-01", endDate = "2028-06-30" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, overlappingBatchResponse.StatusCode);
        var overlappingProblem = await overlappingBatchResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("teacher_contract_conflict", overlappingProblem!.RootElement.GetProperty("code").GetString());

        using var invalidRangeBatchResponse = await _client.PostAsJsonAsync(
            $"/api/teachers/{teacherId}/contracts",
            new { schoolIds = new[] { 1, 2 }, startDate = "2029-12-31", endDate = "2029-01-01" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidRangeBatchResponse.StatusCode);

        var persisted = await _context.TeacherContracts.AsNoTracking().ToListAsync(cancellationToken);
        var contract = Assert.Single(persisted);
        Assert.Equal(
            (teacherId, 1, new DateOnly(2027, 1, 1)),
            (contract.TeacherPersonId, contract.SchoolId, contract.StartDate));
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
