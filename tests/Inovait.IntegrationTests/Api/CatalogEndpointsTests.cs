using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inovait.Api.Contracts;
using Inovait.Core.Domain.Academics;
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
[Trait("Priority", "P0")]
public sealed class CatalogEndpointsTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-CATALOGS")]
    public async Task Catalogs_ReturnCanonicalDtosInDeterministicOrder()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _context.Add(new School("SCH-AUR", "Aurora School", SchoolSector.Private));
        _context.Add(new Grade("G02", "Second Grade", 2));
        _context.Add(new AcademicYear("AY-2025", "Academic Year 2025", new(2025, 1, 1), new(2025, 12, 31)));
        await _context.SaveChangesAsync(cancellationToken);

        var schools = await GetAsync<List<SchoolSummary>>("/api/schools", cancellationToken);
        Assert.Equal([(2, "Aurora School", "Private"), (1, "North Learning Center", "Public")],
            schools.Select(school => (school.Id, school.Name, school.Sector)));

        var grades = await GetAsync<List<GradeSummary>>("/api/grades", cancellationToken);
        Assert.Equal([(1, "First Grade", 1), (2, "Second Grade", 2)],
            grades.Select(grade => (grade.Id, grade.Name, grade.SortOrder)));

        var academicYears = await GetAsync<List<AcademicYearSummary>>("/api/academic-years", cancellationToken);
        Assert.Equal([(1, true), (2, false)], academicYears.Select(year => (year.Id, year.IsCurrent)));
        Assert.True(academicYears[0].StartDate > academicYears[1].StartDate);

        var teacherId = await AddTeacherAsync();
        var teachers = await GetAsync<List<TeacherSummary>>("/api/teachers", cancellationToken);
        Assert.Equal([(teacherId, "CC", "T-CATALOG")], teachers.Select(teacher => (teacher.Id, teacher.DocumentType, teacher.DocumentNumber)));
    }

    [Fact]
    [Trait("Evidence", "IT-CATALOGS")]
    public async Task ClassGroups_FilterAccumulativelyAndReturnEmptyArrayForExistingContextWithoutGroups()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = new ClassGroup(1, 1, 1, "A");
        _context.Add(group);
        _context.Add(new AcademicYear("AY-EMPTY", "Empty Year", new(2024, 1, 1), new(2024, 12, 31)));
        await _context.SaveChangesAsync(cancellationToken);
        var emptyYearId = await _context.AcademicYears.Where(year => year.Code == "AY-EMPTY")
            .Select(year => year.Id).SingleAsync(cancellationToken);

        var matching = await GetAsync<List<ClassGroupSummary>>("/api/class-groups?schoolId=1&gradeId=1&academicYearId=1", cancellationToken);
        Assert.Equal([group.Id], matching.Select(item => item.Id));

        using var emptyContextResponse = await _client.GetAsync(
            $"/api/class-groups?academicYearId={emptyYearId}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, emptyContextResponse.StatusCode);
        Assert.Equal("[]", (await emptyContextResponse.Content.ReadAsStringAsync(cancellationToken)).Trim());

        using var missingSchoolResponse = await _client.GetAsync("/api/class-groups?schoolId=999999", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingSchoolResponse.StatusCode);
        var problem = await missingSchoolResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("school_not_found", problem!.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    [Trait("Evidence", "IT-CATALOGS")]
    public async Task TeachersBySchool_ReflectsEffectiveStatusAndOrdersByTeacherIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sortsLastTeacherId = await AddTeacherAsync("T-LATER", "Zeta", "Alpha");
        var sortsFirstTeacherId = await AddTeacherAsync("T-EARLIER", "Alpha", "Alpha");
        _context.Add(new TeacherContract(sortsLastTeacherId, 1, new(2020, 1, 1), new(2020, 12, 31)));
        _context.Add(new TeacherContract(sortsFirstTeacherId, 1, new(2020, 1, 1), new(2020, 12, 31)));
        await _context.SaveChangesAsync(cancellationToken);

        var teachers = await GetAsync<List<SchoolTeacherSummary>>("/api/schools/1/teachers?asOfDate=2026-01-01", cancellationToken);
        Assert.Equal([sortsFirstTeacherId, sortsLastTeacherId], teachers.Select(item => item.Teacher.Id));
        Assert.All(teachers, item => Assert.Equal("Expired", item.EffectiveStatus));
        Assert.All(teachers, item => Assert.Equal("Confirmed", item.PersistedStatus));

        using var missingSchoolResponse = await _client.GetAsync("/api/schools/999999/teachers", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingSchoolResponse.StatusCode);
    }

    private async Task<T> GetAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(requestUri, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return payload!;
    }

    private async Task<int> AddTeacherAsync(string document = "T-CATALOG", string firstNames = "Test", string lastNames = "Teacher")
    {
        var person = new Person(1, document, firstNames, lastNames, new(1990, 1, 1));
        _context.Add(person);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.Add(new Teacher(person.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return person.Id;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitApiCatalog_{Guid.NewGuid():N}",
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
