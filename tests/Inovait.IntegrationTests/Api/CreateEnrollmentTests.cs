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
