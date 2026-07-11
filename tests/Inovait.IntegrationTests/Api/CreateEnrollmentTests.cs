using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inovait.Api.Contracts;
using Inovait.Core.Domain.Academics;
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
[Trait("Priority", "P0")]
public sealed class CreateEnrollmentTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-ENR-CREATE")]
    public async Task CreateEnrollment_PersistsAtomicallyAndRejectsSecondAnnualEnrollment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = new ClassGroup(1, 1, 1, "A");
        _context.Add(group);
        await _context.SaveChangesAsync(cancellationToken);

        var request = new
        {
            student = new
            {
                documentType = "CC",
                documentNumber = "99.001.101",
                firstNames = "Ana María",
                lastNames = "Solís",
                birthDate = "2018-07-10",
            },
            schoolId = 1,
            academicYearId = 1,
            gradeId = 1,
            classGroupId = group.Id,
        };

        using var createdResponse = await _client.PostAsJsonAsync("/api/enrollments", request, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal("application/json", createdResponse.Content.Headers.ContentType?.MediaType);
        var created = await createdResponse.Content.ReadFromJsonAsync<CreateEnrollmentResponse>(JsonOptions, cancellationToken);
        Assert.NotNull(created);
        Assert.False(created!.StudentReused);
        Assert.Equal("CC", created.DocumentType);
        Assert.Equal("99.001.101", created.DocumentNumber);
        Assert.Equal("Ana María", created.FirstNames);
        Assert.Equal("Solís", created.LastNames);
        Assert.Equal(new DateOnly(2018, 7, 10), created.BirthDate);
        Assert.Equal(ExpectedAge(new DateOnly(2018, 7, 10)), created.Age);
        Assert.Equal((1, "North Learning Center", "Public"), (created.School.Id, created.School.Name, created.School.Sector));
        Assert.Equal((1, "Academic Year 2026", true), (created.AcademicYear.Id, created.AcademicYear.Name, created.AcademicYear.IsCurrent));
        Assert.Equal((1, "First Grade", 1), (created.Grade.Id, created.Grade.Name, created.Grade.SortOrder));
        Assert.Equal((group.Id, "A", 1, 1, 1),
            (created.ClassGroup.Id, created.ClassGroup.Code, created.ClassGroup.SchoolId, created.ClassGroup.AcademicYearId, created.ClassGroup.GradeId));

        using var duplicateResponse = await _client.PostAsJsonAsync("/api/enrollments", request, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal("application/problem+json", duplicateResponse.Content.Headers.ContentType?.MediaType);
        var problem = await duplicateResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("enrollment_conflict", problem!.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, await _context.Enrollments.CountAsync(cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-ENR-FILTER")]
    public async Task ListEnrollments_RequiresExistingContextAndOrdersByIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = new ClassGroup(1, 1, 1, "A");
        _context.Add(group);
        await _context.SaveChangesAsync(cancellationToken);

        await EnrollAsync(group.Id, "Zeta", "Omega", "111", cancellationToken);
        await EnrollAsync(group.Id, "Alpha", "Omega", "222", cancellationToken);

        using var missingParams = await _client.GetAsync("/api/enrollments", cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingParams.StatusCode);
        var validation = await missingParams.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("invalid_request", validation!.RootElement.GetProperty("code").GetString());
        Assert.True(validation.RootElement.GetProperty("errors").TryGetProperty("schoolId", out _));

        using var missingYear = await _client.GetAsync(
            "/api/enrollments?schoolId=1&gradeId=1&academicYearId=999999", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingYear.StatusCode);

        var items = await GetAsync<List<EnrollmentListItem>>(
            "/api/enrollments?schoolId=1&gradeId=1&academicYearId=1", cancellationToken);
        Assert.Equal(["Omega", "Omega"], items.Select(item => item.LastNames));
        Assert.Equal(["Alpha", "Zeta"], items.Select(item => item.FirstNames));

        _context.Add(new AcademicYear("AY-EMPTY-ENR", "Empty Enrollment Year", new(2023, 1, 1), new(2023, 12, 31)));
        await _context.SaveChangesAsync(cancellationToken);
        var emptyYearId = await _context.AcademicYears.Where(year => year.Code == "AY-EMPTY-ENR")
            .Select(year => year.Id).SingleAsync(cancellationToken);
        var empty = await GetAsync<List<EnrollmentListItem>>(
            $"/api/enrollments?schoolId=1&gradeId=1&academicYearId={emptyYearId}", cancellationToken);
        Assert.Empty(empty);
    }

    [Fact]
    [Trait("Evidence", "IT-ENR-IDENTITY")]
    public async Task CreateEnrollment_EquivalentIdentityReusesExistingPersonAndStudent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstGroup = new ClassGroup(1, 1, 1, "ID-A");
        var laterYear = new AcademicYear("AY-ID-REUSE", "Reuse Year", new(2028, 1, 1), new(2028, 12, 31));
        _context.AddRange(firstGroup, laterYear);
        await _context.SaveChangesAsync(cancellationToken);
        var laterGroup = new ClassGroup(1, laterYear.Id, 1, "ID-B");
        _context.Add(laterGroup);
        await _context.SaveChangesAsync(cancellationToken);

        var firstRequest = new
        {
            student = new
            {
                documentType = "CC",
                documentNumber = "77.100.200",
                firstNames = "María José",
                lastNames = "Restrepo",
                birthDate = "2016-03-15",
            },
            schoolId = 1,
            academicYearId = 1,
            gradeId = 1,
            classGroupId = firstGroup.Id,
        };
        using var firstResponse = await _client.PostAsJsonAsync("/api/enrollments", firstRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstCreated = await firstResponse.Content.ReadFromJsonAsync<CreateEnrollmentResponse>(JsonOptions, cancellationToken);
        Assert.NotNull(firstCreated);
        Assert.False(firstCreated!.StudentReused);

        var secondRequest = new
        {
            student = new
            {
                documentType = "CC",
                documentNumber = "77.100.200",
                firstNames = "  MARÍA   JOSÉ ",
                lastNames = "restrepo",
                birthDate = "2016-03-15",
            },
            schoolId = 1,
            academicYearId = laterYear.Id,
            gradeId = 1,
            classGroupId = laterGroup.Id,
        };
        using var secondResponse = await _client.PostAsJsonAsync("/api/enrollments", secondRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var secondCreated = await secondResponse.Content.ReadFromJsonAsync<CreateEnrollmentResponse>(JsonOptions, cancellationToken);
        Assert.NotNull(secondCreated);
        Assert.True(secondCreated!.StudentReused);
        Assert.Equal(firstCreated.StudentId, secondCreated.StudentId);

        Assert.Equal(1, await _context.People.CountAsync(cancellationToken));
        Assert.Equal(1, await _context.Students.CountAsync(cancellationToken));
        Assert.Equal(2, await _context.Enrollments.CountAsync(cancellationToken));
    }

    [Theory]
    [Trait("Evidence", "IT-ENR-IDENTITY")]
    [InlineData("birthDate")]
    [InlineData("lastNames")]
    public async Task CreateEnrollment_DiscrepantIdentity_ReturnsConflictWithoutModifyingExistingPerson(string discrepantField)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstGroup = new ClassGroup(1, 1, 1, $"ID-C-{discrepantField}");
        var secondGroup = new ClassGroup(1, 1, 1, $"ID-D-{discrepantField}");
        _context.AddRange(firstGroup, secondGroup);
        await _context.SaveChangesAsync(cancellationToken);

        var documentNumber = $"77.300.{(discrepantField == "birthDate" ? "301" : "302")}";
        var originalRequest = new
        {
            student = new
            {
                documentType = "CC",
                documentNumber,
                firstNames = "Laura",
                lastNames = "Gómez",
                birthDate = "2014-06-20",
            },
            schoolId = 1,
            academicYearId = 1,
            gradeId = 1,
            classGroupId = firstGroup.Id,
        };
        using var originalResponse = await _client.PostAsJsonAsync("/api/enrollments", originalRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, originalResponse.StatusCode);

        var conflictingRequest = new
        {
            student = new
            {
                documentType = "CC",
                documentNumber,
                firstNames = "Laura",
                lastNames = discrepantField == "lastNames" ? "Gomez" : "Gómez",
                birthDate = discrepantField == "birthDate" ? "2013-06-20" : "2014-06-20",
            },
            schoolId = 1,
            academicYearId = 1,
            gradeId = 1,
            classGroupId = secondGroup.Id,
        };
        using var conflictingResponse = await _client.PostAsJsonAsync("/api/enrollments", conflictingRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, conflictingResponse.StatusCode);
        Assert.Equal("application/problem+json", conflictingResponse.Content.Headers.ContentType?.MediaType);
        var problem = await conflictingResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("enrollment_conflict", problem!.RootElement.GetProperty("code").GetString());
        Assert.Contains("identidad", problem.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        Assert.Equal(1, await _context.People.CountAsync(cancellationToken));
        Assert.Equal(1, await _context.Enrollments.CountAsync(cancellationToken));
        var person = await _context.People.SingleAsync(cancellationToken);
        Assert.Equal("Laura", person.FirstNames);
        Assert.Equal("Gómez", person.LastNames);
        Assert.Equal(new DateOnly(2014, 6, 20), person.BirthDate);
    }

    [Theory]
    [Trait("Evidence", "IT-ENR-CONTEXT")]
    [InlineData("school", "school_not_found")]
    [InlineData("grade", "grade_not_found")]
    [InlineData("academicYear", "academic_year_not_found")]
    [InlineData("classGroup", "class_group_not_found")]
    public async Task CreateEnrollment_MissingCatalogReference_ReturnsNotFoundWithoutPartialPersistence(
        string missingReference, string expectedCode)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = new ClassGroup(1, 1, 1, $"CTX-{missingReference}");
        _context.Add(group);
        await _context.SaveChangesAsync(cancellationToken);

        var request = new
        {
            student = new
            {
                documentType = "CC",
                documentNumber = $"CTX-{missingReference}",
                firstNames = "Ctx",
                lastNames = "Missing",
                birthDate = "2015-01-01",
            },
            schoolId = missingReference == "school" ? 999999 : 1,
            academicYearId = missingReference == "academicYear" ? 999999 : 1,
            gradeId = missingReference == "grade" ? 999999 : 1,
            classGroupId = missingReference == "classGroup" ? 999999 : group.Id,
        };

        using var response = await _client.PostAsJsonAsync("/api/enrollments", request, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal(expectedCode, problem!.RootElement.GetProperty("code").GetString());

        Assert.Equal(0, await _context.People.CountAsync(cancellationToken));
        Assert.Equal(0, await _context.Students.CountAsync(cancellationToken));
        Assert.Equal(0, await _context.Enrollments.CountAsync(cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-ENR-CONTEXT")]
    public async Task CreateEnrollment_ClassGroupOutsideRequestedContext_ReturnsUnprocessableEntityWithoutPartialPersistence()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var otherSchool = new School("SCH-CTX-422", "Context Mismatch School", SchoolSector.Public);
        _context.Add(otherSchool);
        var group = new ClassGroup(1, 1, 1, "CTX-422");
        _context.Add(group);
        await _context.SaveChangesAsync(cancellationToken);

        var request = new
        {
            student = new
            {
                documentType = "CC",
                documentNumber = "CTX-422",
                firstNames = "Ctx",
                lastNames = "Mismatch",
                birthDate = "2015-01-01",
            },
            schoolId = otherSchool.Id,
            academicYearId = 1,
            gradeId = 1,
            classGroupId = group.Id,
        };
        using var response = await _client.PostAsJsonAsync("/api/enrollments", request, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("academic_context_invalid", problem!.RootElement.GetProperty("code").GetString());

        Assert.Equal(0, await _context.People.CountAsync(cancellationToken));
        Assert.Equal(0, await _context.Enrollments.CountAsync(cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-ENR-CONTEXT")]
    public async Task CreateEnrollment_FutureBirthDate_ReturnsUnprocessableEntityWithoutPartialPersistence()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = new ClassGroup(1, 1, 1, "CTX-FUTURE");
        _context.Add(group);
        await _context.SaveChangesAsync(cancellationToken);

        var futureBirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var request = new
        {
            student = new
            {
                documentType = "CC",
                documentNumber = "CTX-FUTURE",
                firstNames = "Ctx",
                lastNames = "Future",
                birthDate = futureBirthDate.ToString("yyyy-MM-dd"),
            },
            schoolId = 1,
            academicYearId = 1,
            gradeId = 1,
            classGroupId = group.Id,
        };
        using var response = await _client.PostAsJsonAsync("/api/enrollments", request, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("invalid_birth_date", problem!.RootElement.GetProperty("code").GetString());

        Assert.Equal(0, await _context.People.CountAsync(cancellationToken));
        Assert.Equal(0, await _context.Enrollments.CountAsync(cancellationToken));
    }

    private async Task EnrollAsync(
        int classGroupId, string firstNames, string lastNames, string documentNumber, CancellationToken cancellationToken)
    {
        var request = new
        {
            student = new { documentType = "CC", documentNumber, firstNames, lastNames, birthDate = "2015-05-05" },
            schoolId = 1,
            academicYearId = 1,
            gradeId = 1,
            classGroupId,
        };
        using var response = await _client.PostAsJsonAsync("/api/enrollments", request, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<T> GetAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(requestUri, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return payload!;
    }

    private static int ExpectedAge(DateOnly birthDate)
    {
        var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = asOfDate.Year - birthDate.Year;
        if (birthDate > asOfDate.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitApiEnrollment_{Guid.NewGuid():N}",
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
