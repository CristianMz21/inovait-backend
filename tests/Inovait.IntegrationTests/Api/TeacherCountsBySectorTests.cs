using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inovait.Api.Contracts;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.People;
using Inovait.Core.Domain.Staff;
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
public sealed class TeacherCountsBySectorTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-RPT-SECTOR")]
    public async Task TeacherCounts_CountsDistinctTeachersPerSectorAndZeroFillsBothSectors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _context.Add(new School("SCH-AUR", "Aurora School", SchoolSector.Private));
        _context.Add(new School("SCH-PUB2", "North Annex", SchoolSector.Public));
        await _context.SaveChangesAsync(cancellationToken);
        var privateSchoolId = await SchoolIdByCodeAsync("SCH-AUR", cancellationToken);
        var secondPublicSchoolId = await SchoolIdByCodeAsync("SCH-PUB2", cancellationToken);

        var teacherA = await AddTeacherAsync("T-DIST-A", cancellationToken);
        var teacherB = await AddTeacherAsync("T-DIST-B", cancellationToken);
        await AddContractAsync(teacherA, 1, new(2026, 1, 1), new(2026, 6, 30), cancellationToken);
        await AddContractAsync(teacherA, secondPublicSchoolId, new(2026, 7, 1), new(2026, 12, 31), cancellationToken);
        await AddContractAsync(teacherB, 1, new(2026, 1, 1), null, cancellationToken);
        await AddContractAsync(teacherB, privateSchoolId, new(2026, 1, 1), null, cancellationToken);

        var withActivity = await GetAsync<TeacherCountsBySectorResponse>(
            "/api/reports/teacher-counts-by-sector?periodStart=2026-01-01&periodEnd=2026-12-31", cancellationToken);
        Assert.Equal(2, withActivity.PublicDistinctTeacherCount);
        Assert.Equal(1, withActivity.PrivateDistinctTeacherCount);

        var withoutActivity = await GetAsync<TeacherCountsBySectorResponse>(
            "/api/reports/teacher-counts-by-sector?periodStart=2020-01-01&periodEnd=2020-01-02", cancellationToken);
        Assert.Equal(0, withoutActivity.PublicDistinctTeacherCount);
        Assert.Equal(0, withoutActivity.PrivateDistinctTeacherCount);
    }

    [Fact]
    [Trait("Evidence", "IT-RPT-SECTOR")]
    public async Task TeacherCounts_ExcludesCancelledContractsAndHonorsInclusivePeriodEdges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var edgeTeacher = await AddTeacherAsync("T-EDGE", cancellationToken);
        await AddContractAsync(edgeTeacher, 1, new(2026, 1, 1), new(2026, 6, 30), cancellationToken);

        var touchingEdge = await GetAsync<TeacherCountsBySectorResponse>(
            "/api/reports/teacher-counts-by-sector?periodStart=2026-06-30&periodEnd=2026-07-05", cancellationToken);
        Assert.Equal(1, touchingEdge.PublicDistinctTeacherCount);

        var afterEdge = await GetAsync<TeacherCountsBySectorResponse>(
            "/api/reports/teacher-counts-by-sector?periodStart=2026-07-01&periodEnd=2026-07-05", cancellationToken);
        Assert.Equal(0, afterEdge.PublicDistinctTeacherCount);

        var cancelledTeacher = await AddTeacherAsync("T-CANCEL", cancellationToken);
        var contract = await AddContractAsync(cancelledTeacher, 1, new(2026, 8, 1), new(2026, 12, 31), cancellationToken);
        contract.Cancel(DateTime.UtcNow, "Renuncia", new(2026, 9, 1));
        await _context.SaveChangesAsync(cancellationToken);

        // Period is fully before the cancellation effective date and inside the contract's original
        // [StartDate, EndDate] range, and does not overlap edgeTeacher's contract: a persisted Cancelled
        // status excludes the contract outright, regardless of where the cancellation effective date falls.
        var beforeCancellationEffectiveDate = await GetAsync<TeacherCountsBySectorResponse>(
            "/api/reports/teacher-counts-by-sector?periodStart=2026-08-01&periodEnd=2026-08-31", cancellationToken);
        Assert.Equal(0, beforeCancellationEffectiveDate.PublicDistinctTeacherCount);
    }

    [Fact]
    [Trait("Evidence", "IT-RPT-SECTOR")]
    public async Task TeacherCounts_DefaultsToCurrentDateSinglePeriodWhenOmitted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await AddTeacherAsync("T-TODAY", cancellationToken);
        await AddContractAsync(teacher, 1, new(2020, 1, 1), null, cancellationToken);

        var response = await GetAsync<TeacherCountsBySectorResponse>("/api/reports/teacher-counts-by-sector", cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(today, response.PeriodStart);
        Assert.Equal(today, response.PeriodEnd);
        Assert.Equal(1, response.PublicDistinctTeacherCount);
        Assert.Equal(0, response.PrivateDistinctTeacherCount);
    }

    [Fact]
    [Trait("Evidence", "IT-RPT-SECTOR")]
    public async Task TeacherCounts_RejectsPartialPeriodAndInvertedRangeWithProblemDetailsShape()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var onlyStart = await _client.GetAsync(
            "/api/reports/teacher-counts-by-sector?periodStart=2026-01-01", cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, onlyStart.StatusCode);
        var onlyStartProblem = await onlyStart.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("invalid_request", onlyStartProblem!.RootElement.GetProperty("code").GetString());
        Assert.True(onlyStartProblem.RootElement.GetProperty("errors").TryGetProperty("periodEnd", out _));

        using var onlyEnd = await _client.GetAsync(
            "/api/reports/teacher-counts-by-sector?periodEnd=2026-01-01", cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, onlyEnd.StatusCode);
        var onlyEndProblem = await onlyEnd.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("invalid_request", onlyEndProblem!.RootElement.GetProperty("code").GetString());
        Assert.True(onlyEndProblem.RootElement.GetProperty("errors").TryGetProperty("periodStart", out _));

        using var inverted = await _client.GetAsync(
            "/api/reports/teacher-counts-by-sector?periodStart=2026-12-31&periodEnd=2026-01-01", cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inverted.StatusCode);
        Assert.Equal("application/problem+json", inverted.Content.Headers.ContentType?.MediaType);
        var invertedBody = await inverted.Content.ReadAsStringAsync(cancellationToken);
        using var invertedDocument = JsonDocument.Parse(invertedBody);
        var invertedRoot = invertedDocument.RootElement;
        Assert.True(invertedRoot.TryGetProperty("type", out _));
        Assert.True(invertedRoot.TryGetProperty("title", out _));
        Assert.Equal(422, invertedRoot.GetProperty("status").GetInt32());
        Assert.Equal("period_invalid", invertedRoot.GetProperty("code").GetString());
        Assert.True(invertedRoot.GetProperty("errors").TryGetProperty("periodEnd", out _));
    }

    private async Task<int> SchoolIdByCodeAsync(string code, CancellationToken cancellationToken) =>
        await _context.Schools.Where(school => school.Code == code).Select(school => school.Id).SingleAsync(cancellationToken);

    private async Task<int> AddTeacherAsync(string documentNumber, CancellationToken cancellationToken)
    {
        var person = new Person(1, documentNumber, "Test", "Teacher", new(1990, 1, 1));
        _context.Add(person);
        await _context.SaveChangesAsync(cancellationToken);
        _context.Add(new Teacher(person.Id));
        await _context.SaveChangesAsync(cancellationToken);
        return person.Id;
    }

    private async Task<TeacherContract> AddContractAsync(
        int teacherPersonId, int schoolId, DateOnly startDate, DateOnly? endDate, CancellationToken cancellationToken)
    {
        var contract = new TeacherContract(teacherPersonId, schoolId, startDate, endDate);
        _context.Add(contract);
        await _context.SaveChangesAsync(cancellationToken);
        return contract;
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
            InitialCatalog = $"InovaitApiTeacherSector_{Guid.NewGuid():N}",
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
