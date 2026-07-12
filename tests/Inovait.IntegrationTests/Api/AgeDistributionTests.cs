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
public sealed class AgeDistributionTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateOnly AsOfDate = new(2026, 7, 10);

    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-RPT-AGE")]
    public async Task AgeDistribution_ComputesBoundaryBucketsAndExcludesUnderThree()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = new ClassGroup(1, 1, 1, "A");
        _context.Add(group);
        await _context.SaveChangesAsync(cancellationToken);

        await EnrollStudentAsync("AGE-02", AsOfDate.AddYears(-2), group.Id, cancellationToken);
        await EnrollStudentAsync("AGE-03", AsOfDate.AddYears(-3), group.Id, cancellationToken);
        await EnrollStudentAsync("AGE-07", AsOfDate.AddYears(-7), group.Id, cancellationToken);
        await EnrollStudentAsync("AGE-08", AsOfDate.AddYears(-8), group.Id, cancellationToken);
        await EnrollStudentAsync("AGE-12", AsOfDate.AddYears(-12), group.Id, cancellationToken);
        await EnrollStudentAsync("AGE-13", AsOfDate.AddYears(-13), group.Id, cancellationToken);

        var response = await GetAsync<AgeDistributionResponse>(
            $"/api/reports/age-distribution?academicYearId=1&asOfDate={AsOfDate:yyyy-MM-dd}", cancellationToken);

        Assert.Equal(1, response.AcademicYearId);
        Assert.Null(response.SchoolId);
        Assert.Null(response.GradeId);
        Assert.Equal(AsOfDate, response.AsOfDate);
        Assert.Equal((3, 7, 2), (response.Age3To7.MinimumAge, response.Age3To7.MaximumAge, response.Age3To7.Count));
        Assert.Equal((8, 12, 2), (response.Age8To12.MinimumAge, response.Age8To12.MaximumAge, response.Age8To12.Count));
        Assert.Equal((13, (int?)null, 1), (response.AgeOver12.MinimumAge, response.AgeOver12.MaximumAge, response.AgeOver12.Count));
    }

    [Fact]
    [Trait("Evidence", "IT-RPT-AGE")]
    public async Task AgeDistribution_FiltersAccumulativelyAndReturnsZeroForExistingContextWithoutEnrollments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _context.Add(new School("SCH-AUR", "Aurora School", SchoolSector.Private));
        _context.Add(new Grade("G02", "Second Grade", 2));
        await _context.SaveChangesAsync(cancellationToken);

        var groupSchool1Grade1 = new ClassGroup(1, 1, 1, "A");
        var groupSchool2Grade1 = new ClassGroup(2, 1, 1, "B");
        var groupSchool1Grade2 = new ClassGroup(1, 1, 2, "C");
        _context.AddRange(groupSchool1Grade1, groupSchool2Grade1, groupSchool1Grade2);
        await _context.SaveChangesAsync(cancellationToken);

        await EnrollStudentAsync("FILT-A", AsOfDate.AddYears(-3), groupSchool1Grade1.Id, cancellationToken);
        await EnrollStudentAsync("FILT-B", AsOfDate.AddYears(-8), groupSchool2Grade1.Id, cancellationToken);
        await EnrollStudentAsync("FILT-C", AsOfDate.AddYears(-13), groupSchool1Grade2.Id, cancellationToken);

        var all = await GetAsync<AgeDistributionResponse>(
            $"/api/reports/age-distribution?academicYearId=1&asOfDate={AsOfDate:yyyy-MM-dd}", cancellationToken);
        Assert.Equal((1, 1, 1), (all.Age3To7.Count, all.Age8To12.Count, all.AgeOver12.Count));

        var bySchool = await GetAsync<AgeDistributionResponse>(
            $"/api/reports/age-distribution?academicYearId=1&schoolId=1&asOfDate={AsOfDate:yyyy-MM-dd}", cancellationToken);
        Assert.Equal((1, 0, 1), (bySchool.Age3To7.Count, bySchool.Age8To12.Count, bySchool.AgeOver12.Count));

        var byGrade = await GetAsync<AgeDistributionResponse>(
            $"/api/reports/age-distribution?academicYearId=1&gradeId=1&asOfDate={AsOfDate:yyyy-MM-dd}", cancellationToken);
        Assert.Equal((1, 1, 0), (byGrade.Age3To7.Count, byGrade.Age8To12.Count, byGrade.AgeOver12.Count));

        var bySchoolAndGrade = await GetAsync<AgeDistributionResponse>(
            $"/api/reports/age-distribution?academicYearId=1&schoolId=1&gradeId=1&asOfDate={AsOfDate:yyyy-MM-dd}", cancellationToken);
        Assert.Equal((1, 0, 0), (bySchoolAndGrade.Age3To7.Count, bySchoolAndGrade.Age8To12.Count, bySchoolAndGrade.AgeOver12.Count));

        var existingContextWithoutEnrollments = await GetAsync<AgeDistributionResponse>(
            $"/api/reports/age-distribution?academicYearId=1&schoolId=2&gradeId=2&asOfDate={AsOfDate:yyyy-MM-dd}", cancellationToken);
        Assert.Equal((0, 0, 0),
            (existingContextWithoutEnrollments.Age3To7.Count, existingContextWithoutEnrollments.Age8To12.Count,
                existingContextWithoutEnrollments.AgeOver12.Count));
    }

    [Fact]
    [Trait("Evidence", "IT-RPT-AGE")]
    public async Task AgeDistribution_UsesCurrentDateWhenAsOfDateOmittedAndZeroesAnEmptyYear()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _context.Add(new AcademicYear("AY-EMPTY", "Empty Year", new(2024, 1, 1), new(2024, 12, 31)));
        await _context.SaveChangesAsync(cancellationToken);
        var emptyYearId = await _context.AcademicYears.Where(year => year.Code == "AY-EMPTY")
            .Select(year => year.Id).SingleAsync(cancellationToken);

        var response = await GetAsync<AgeDistributionResponse>(
            $"/api/reports/age-distribution?academicYearId={emptyYearId}", cancellationToken);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), response.AsOfDate);
        Assert.Equal((0, 0, 0), (response.Age3To7.Count, response.Age8To12.Count, response.AgeOver12.Count));
    }

    [Fact]
    [Trait("Evidence", "IT-RPT-AGE")]
    public async Task AgeDistribution_RejectsMissingReferences()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var missingAcademicYear = await _client.GetAsync(
            "/api/reports/age-distribution?academicYearId=999999", cancellationToken);
        await AssertProblemAsync(missingAcademicYear, HttpStatusCode.NotFound, "academic_year_not_found");

        using var missingSchool = await _client.GetAsync(
            "/api/reports/age-distribution?academicYearId=1&schoolId=999999", cancellationToken);
        await AssertProblemAsync(missingSchool, HttpStatusCode.NotFound, "school_not_found");

        using var missingGrade = await _client.GetAsync(
            "/api/reports/age-distribution?academicYearId=1&gradeId=999999", cancellationToken);
        await AssertProblemAsync(missingGrade, HttpStatusCode.NotFound, "grade_not_found");

        using var missingRequiredYear = await _client.GetAsync("/api/reports/age-distribution", cancellationToken);
        await AssertProblemAsync(missingRequiredYear, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Fact]
    [Trait("Evidence", "IT-RPT-AGE")]
    public async Task AgeDistribution_RejectsAsOfDatePrecedingAnIncludedBirthWithProblemDetailsShape()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = new ClassGroup(1, 1, 1, "A");
        _context.Add(group);
        await _context.SaveChangesAsync(cancellationToken);
        await EnrollStudentAsync("AGE-FUT", new(2020, 1, 1), group.Id, cancellationToken);

        using var response = await _client.GetAsync(
            "/api/reports/age-distribution?academicYearId=1&asOfDate=2019-12-31", cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("type", out _));
        Assert.True(root.TryGetProperty("title", out _));
        Assert.Equal(422, root.GetProperty("status").GetInt32());
        Assert.Equal("as_of_date_invalid", root.GetProperty("code").GetString());
    }

    private async Task EnrollStudentAsync(
        string documentNumber, DateOnly birthDate, int classGroupId, CancellationToken cancellationToken)
    {
        var person = new Person(1, documentNumber, "Test", "Student", birthDate);
        _context.Add(person);
        await _context.SaveChangesAsync(cancellationToken);
        _context.Add(new Student(person.Id));
        _context.Add(new Enrollment(person.Id, classGroupId, 1));
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<T> GetAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(requestUri, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return payload!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string expectedCode)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        Assert.Equal((int)status, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitApiAgeDistribution_{Guid.NewGuid():N}",
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
