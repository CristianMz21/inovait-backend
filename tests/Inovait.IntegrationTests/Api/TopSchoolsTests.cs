using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inovait.Api.Contracts;
using Inovait.Core.Domain.Academics;
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
[Trait("Priority", "P1")]
public sealed class TopSchoolsTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-RPT-TOP")]
    public async Task TopSchools_ReturnsAllSchoolsTiedAtMaximumOrderedByNameThenId()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _context.Add(new School("SCH-ZULU", "Zulu School", SchoolSector.Private));
        _context.Add(new School("SCH-BETA", "Beta School", SchoolSector.Public));
        _context.Add(new School("SCH-GAMMA", "Gamma School", SchoolSector.Private));
        await _context.SaveChangesAsync(cancellationToken);
        var zuluId = await SchoolIdByCodeAsync("SCH-ZULU", cancellationToken);
        var betaId = await SchoolIdByCodeAsync("SCH-BETA", cancellationToken);
        var gammaId = await SchoolIdByCodeAsync("SCH-GAMMA", cancellationToken);

        await EnrollCountAsync(1, 2, cancellationToken); // North Learning Center, not tied at max
        await EnrollCountAsync(zuluId, 3, cancellationToken); // tied at max
        await EnrollCountAsync(betaId, 3, cancellationToken); // tied at max
        await EnrollCountAsync(gammaId, 1, cancellationToken); // not tied at max

        var response = await GetAsync<List<TopSchoolResponse>>(
            "/api/reports/top-schools?academicYearId=1", cancellationToken);
        Assert.Equal(
            [(betaId, "Beta School", 3), (zuluId, "Zulu School", 3)],
            response.Select(item => (item.School.Id, item.School.Name, item.EnrollmentCount)));
        Assert.All(response, item => Assert.Equal(1, item.AcademicYearId));
    }

    [Fact]
    [Trait("Evidence", "IT-RPT-TOP")]
    public async Task TopSchools_ReturnsEmptyArrayForYearWithoutEnrollments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _context.Add(new AcademicYear("AY-EMPTY", "Empty Year", new(2024, 1, 1), new(2024, 12, 31)));
        await _context.SaveChangesAsync(cancellationToken);
        var emptyYearId = await _context.AcademicYears.Where(year => year.Code == "AY-EMPTY")
            .Select(year => year.Id).SingleAsync(cancellationToken);

        using var response = await _client.GetAsync($"/api/reports/top-schools?academicYearId={emptyYearId}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", (await response.Content.ReadAsStringAsync(cancellationToken)).Trim());
    }

    [Fact]
    [Trait("Evidence", "IT-RPT-TOP")]
    public async Task TopSchools_RequiresAcademicYearIdAndRejectsMissingYear()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var missingParam = await _client.GetAsync("/api/reports/top-schools", cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingParam.StatusCode);
        var missingParamProblem = await missingParam.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("invalid_request", missingParamProblem!.RootElement.GetProperty("code").GetString());

        using var invalidParam = await _client.GetAsync("/api/reports/top-schools?academicYearId=0", cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidParam.StatusCode);

        using var missingYear = await _client.GetAsync("/api/reports/top-schools?academicYearId=999999", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingYear.StatusCode);
        Assert.Equal("application/problem+json", missingYear.Content.Headers.ContentType?.MediaType);
        var missingYearBody = await missingYear.Content.ReadAsStringAsync(cancellationToken);
        using var missingYearDocument = JsonDocument.Parse(missingYearBody);
        var missingYearRoot = missingYearDocument.RootElement;
        Assert.True(missingYearRoot.TryGetProperty("type", out _));
        Assert.True(missingYearRoot.TryGetProperty("title", out _));
        Assert.Equal(404, missingYearRoot.GetProperty("status").GetInt32());
        Assert.Equal("academic_year_not_found", missingYearRoot.GetProperty("code").GetString());
    }

    private async Task<int> SchoolIdByCodeAsync(string code, CancellationToken cancellationToken) =>
        await _context.Schools.Where(school => school.Code == code).Select(school => school.Id).SingleAsync(cancellationToken);

    private async Task EnrollCountAsync(int schoolId, int count, CancellationToken cancellationToken)
    {
        var group = new ClassGroup(schoolId, 1, 1, $"CG-{schoolId}");
        _context.Add(group);
        await _context.SaveChangesAsync(cancellationToken);
        for (var index = 0; index < count; index++)
        {
            var person = new Person(1, $"TOP-{schoolId}-{index}", "Test", "Student", new(2015, 1, 1));
            _context.Add(person);
            await _context.SaveChangesAsync(cancellationToken);
            _context.Add(new Student(person.Id));
            _context.Add(new Enrollment(person.Id, group.Id, 1));
            await _context.SaveChangesAsync(cancellationToken);
        }
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
            InitialCatalog = $"InovaitApiTopSchools_{Guid.NewGuid():N}",
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
