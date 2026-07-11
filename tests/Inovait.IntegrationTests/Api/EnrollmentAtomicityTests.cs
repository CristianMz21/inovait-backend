using System.Net;
using System.Net.Http.Json;
using Inovait.Core.Domain.Academics;
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
public sealed class EnrollmentAtomicityTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-ENR-ATOMIC")]
    public async Task CreateEnrollment_EnrollmentInsertFailure_RollsBackPersonAndStudentCreation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var group = new ClassGroup(1, 1, 1, "ATOMIC-ROLLBACK");
        _context.Add(group);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(
            "CREATE OR ALTER TRIGGER [academic].[TR_Enrollment_ForceRollback] ON [academic].[Enrollment] " +
            "AFTER INSERT AS THROW 51021,'Injected enrollment failure.',1", cancellationToken);
        try
        {
            var request = new
            {
                student = new
                {
                    documentType = "CC",
                    documentNumber = "ATOMIC-ROLLBACK-1",
                    firstNames = "Atomic",
                    lastNames = "Rollback",
                    birthDate = "2015-01-01",
                },
                schoolId = 1,
                academicYearId = 1,
                gradeId = 1,
                classGroupId = group.Id,
            };
            using var response = await _client.PostAsJsonAsync("/api/enrollments", request, cancellationToken);
            Assert.False(response.IsSuccessStatusCode);

            Assert.Equal(0, await _context.People.CountAsync(cancellationToken));
            Assert.Equal(0, await _context.Students.CountAsync(cancellationToken));
            Assert.Equal(0, await _context.Enrollments.CountAsync(cancellationToken));
        }
        finally
        {
            await _context.Database.ExecuteSqlRawAsync(
                "DROP TRIGGER [academic].[TR_Enrollment_ForceRollback]", CancellationToken.None);
        }
    }

    [Fact]
    [Trait("Evidence", "IT-ENR-ATOMIC")]
    public async Task CreateEnrollment_ConcurrentRequestsForSameIdentityAndYear_CommitExactlyOneEnrollment()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstGroup = new ClassGroup(1, 1, 1, "RACE-A");
        var secondGroup = new ClassGroup(1, 1, 1, "RACE-B");
        _context.AddRange(firstGroup, secondGroup);
        await _context.SaveChangesAsync(cancellationToken);

        static object BuildRequest(int classGroupId) => new
        {
            student = new
            {
                documentType = "CC",
                documentNumber = "RACE-2026",
                firstNames = "Race",
                lastNames = "Condition",
                birthDate = "2015-01-01",
            },
            schoolId = 1,
            academicYearId = 1,
            gradeId = 1,
            classGroupId,
        };

        var responses = await Task.WhenAll(
            _client.PostAsJsonAsync("/api/enrollments", BuildRequest(firstGroup.Id), cancellationToken),
            _client.PostAsJsonAsync("/api/enrollments", BuildRequest(secondGroup.Id), cancellationToken));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        var statusCodes = new[] { firstResponse.StatusCode, secondResponse.StatusCode };
        Assert.Single(statusCodes, code => code == HttpStatusCode.Created);
        Assert.Single(statusCodes, code => code == HttpStatusCode.Conflict);

        Assert.Equal(1, await _context.People.CountAsync(cancellationToken));
        Assert.Equal(1, await _context.Students.CountAsync(cancellationToken));
        Assert.Equal(1, await _context.Enrollments.CountAsync(cancellationToken));
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitApiEnrollmentAtomicity_{Guid.NewGuid():N}",
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
