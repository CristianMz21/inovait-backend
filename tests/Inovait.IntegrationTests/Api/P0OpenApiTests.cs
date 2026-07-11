using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.Catalogs;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Persistence;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inovait.IntegrationTests.Api;

[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P0")]
public sealed class P0OpenApiTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly string[] ForbiddenSubstrings =
    [
        "Exception", "StackTrace", "at Inovait.", "Microsoft.Data.SqlClient", "System.Data",
        "SELECT ", "INSERT ", "UPDATE ", "DELETE FROM",
    ];

    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-PROBLEMS")]
    public async Task ProblemDetailsResponses_UseStableShapeForEachHttpStatusWithoutInternalDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var secondSchool = new School("SCH-CTX", "Context School", SchoolSector.Private);
        _context.Add(secondSchool);
        var group = new ClassGroup(1, 1, 1, "A");
        _context.Add(group);
        await _context.SaveChangesAsync(cancellationToken);

        await AssertProblemAsync(await _client.GetAsync("/api/enrollments", cancellationToken),
            HttpStatusCode.BadRequest, "invalid_request");

        await AssertProblemAsync(await _client.GetAsync("/api/teachers/999999/contracts", cancellationToken),
            HttpStatusCode.NotFound, "teacher_not_found");

        var enrollmentRequest = new
        {
            student = new
            {
                documentType = "CC", documentNumber = "77.001.001", firstNames = "Duplicate", lastNames = "Case",
                birthDate = "2015-01-01",
            },
            schoolId = 1,
            academicYearId = 1,
            gradeId = 1,
            classGroupId = group.Id,
        };
        using var firstCreate = await _client.PostAsJsonAsync("/api/enrollments", enrollmentRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstCreate.StatusCode);
        await AssertProblemAsync(await _client.PostAsJsonAsync("/api/enrollments", enrollmentRequest, cancellationToken),
            HttpStatusCode.Conflict, "enrollment_conflict");

        var mismatchedContextRequest = new
        {
            student = new
            {
                documentType = "CC", documentNumber = "77.002.002", firstNames = "Mismatch", lastNames = "Case",
                birthDate = "2015-01-01",
            },
            schoolId = secondSchool.Id,
            academicYearId = 1,
            gradeId = 1,
            classGroupId = group.Id,
        };
        await AssertProblemAsync(await _client.PostAsJsonAsync("/api/enrollments", mismatchedContextRequest, cancellationToken),
            HttpStatusCode.UnprocessableEntity, "academic_context_invalid");
    }

    [Fact]
    [Trait("Evidence", "IT-OPENAPI-P0")]
    public async Task ContractBundleIsUntouchedAndExactlyTenP0OperationIdsAreRuntimeMapped()
    {
        var repositoryRoot = (await RunGitAsync(AppContext.BaseDirectory, "rev-parse --show-toplevel")).Trim();
        var status = await RunGitAsync(repositoryRoot, "status --porcelain -- specs");
        Assert.Equal(string.Empty, status.Trim());

        var endpointDataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var operationIds = endpointDataSource.Endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .Where(name => name is not null)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expected =
        [
            "createEnrollment", "createTeacherContracts", "listAcademicYears", "listClassGroups", "listEnrollments",
            "listGrades", "listSchools", "listTeacherContracts", "listTeachers", "listTeachersBySchool",
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), operationIds);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string expectedCode)
    {
        using var response1 = response;
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = System.Text.Json.JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("type", out _));
        Assert.True(root.TryGetProperty("title", out _));
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, root.GetProperty("code").GetString());
        Assert.All(ForbiddenSubstrings, forbidden => Assert.DoesNotContain(forbidden, body, StringComparison.Ordinal));
    }

    private static async Task<string> RunGitAsync(string workingDirectory, string arguments)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitApiOpenApi_{Guid.NewGuid():N}",
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
