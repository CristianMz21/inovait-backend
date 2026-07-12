using System.Diagnostics;
using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.People;
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
[Trait("Priority", "P1")]
public sealed class P1OpenApiTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-OPENAPI")]
    public async Task ContractBundleIsUntouchedAndExactlyFifteenOperationIdsAreRuntimeMapped()
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

        // The full canonical bundle: the 10 P0 operationIds plus the 5 P1 operationIds
        // (getAgeDistribution, getDistinctTeacherCountsBySector, getStudentHistory,
        // getTopSchoolsByEnrollment, listSubjects). Now that every P1 slice is wired up end to
        // end, runtime must map this set exactly -- no more, no less, no duplicates.
        string[] canonicalBundle =
        [
            "createEnrollment", "createTeacherContracts", "getAgeDistribution", "getDistinctTeacherCountsBySector",
            "getStudentHistory", "getTopSchoolsByEnrollment", "listAcademicYears", "listClassGroups", "listEnrollments",
            "listGrades", "listSchools", "listSubjects", "listTeacherContracts", "listTeachers", "listTeachersBySchool",
        ];
        Assert.Equal(15, canonicalBundle.Length);
        Assert.Equal(canonicalBundle.Order(StringComparer.Ordinal), operationIds);
    }

    [Fact]
    [Trait("Evidence", "IT-OPENAPI")]
    public async Task NewP1OperationsRespondWithoutServerErrorForWireUpSanity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _context.Add(new Subject("SUB-WIRE", "Wiring Subject"));
        var group = new ClassGroup(1, 1, 1, "WIRE-A");
        _context.Add(group);
        var person = new Person(1, "70.700.700", "Wire", "Sanity", new DateOnly(2015, 1, 1));
        _context.Add(person);
        await _context.SaveChangesAsync(cancellationToken);
        _context.Add(new Student(person.Id));
        await _context.SaveChangesAsync(cancellationToken);
        _context.Add(new Enrollment(person.Id, group.Id, 1));
        await _context.SaveChangesAsync(cancellationToken);

        // One probe per P1-only operationId, proving each is actually wired end-to-end at
        // runtime. Detailed behavior for each is asserted by its dedicated evidence test
        // (IT-LIST-SUBJECTS, IT-RPT-AGE, IT-RPT-SECTOR, IT-RPT-TOP, IT-HISTORY); this only
        // guards against a 500 from a missing DI registration, mapping typo, or similar wiring
        // regression.
        await AssertNotServerErrorAsync("/api/subjects", cancellationToken);
        await AssertNotServerErrorAsync("/api/reports/age-distribution?academicYearId=1", cancellationToken);
        await AssertNotServerErrorAsync("/api/reports/teacher-counts-by-sector", cancellationToken);
        await AssertNotServerErrorAsync("/api/reports/top-schools?academicYearId=1", cancellationToken);
        await AssertNotServerErrorAsync("/api/students/CC/70.700.700/history", cancellationToken);
    }

    private async Task AssertNotServerErrorAsync(string requestUri, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(requestUri, cancellationToken);
        Assert.True(
            (int)response.StatusCode < 500,
            $"{requestUri} returned {(int)response.StatusCode} {response.StatusCode}, expected non-500.");
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
            InitialCatalog = $"InovaitApiOpenApiP1_{Guid.NewGuid():N}",
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
