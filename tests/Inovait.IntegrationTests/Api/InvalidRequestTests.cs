using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
public sealed class InvalidRequestTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-PROBLEMS")]
    public async Task MissingRequiredStartDate_ReturnsBadRequestWithoutPersistingContract()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.PostAsJsonAsync(
            "/api/teachers/5/contracts", new { schoolIds = new[] { 1 } }, cancellationToken);
        await AssertInvalidRequestAsync(response, cancellationToken);
        Assert.Equal(0, await _context.TeacherContracts.CountAsync(cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-PROBLEMS")]
    public async Task MissingRequiredBirthDate_ReturnsBadRequestWithoutPersistingPerson()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = new
        {
            student = new { documentType = "CC", documentNumber = "55.001.001", firstNames = "Sin", lastNames = "Fecha" },
            schoolId = 1,
            academicYearId = 1,
            gradeId = 1,
            classGroupId = 1,
        };
        using var response = await _client.PostAsJsonAsync("/api/enrollments", request, cancellationToken);
        await AssertInvalidRequestAsync(response, cancellationToken);
        Assert.Equal(0, await _context.People.CountAsync(cancellationToken));
        Assert.Equal(0, await _context.Enrollments.CountAsync(cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-PROBLEMS")]
    public async Task MissingRequiredIntReference_ReturnsBadRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = new
        {
            student = new
            {
                documentType = "CC",
                documentNumber = "55.002.002",
                firstNames = "Sin",
                lastNames = "Escuela",
                birthDate = "2015-01-01",
            },
            academicYearId = 1,
            gradeId = 1,
            classGroupId = 1,
        };
        using var response = await _client.PostAsJsonAsync("/api/enrollments", request, cancellationToken);
        await AssertInvalidRequestAsync(response, cancellationToken);
        Assert.Equal(0, await _context.People.CountAsync(cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-PROBLEMS")]
    public async Task UnparseableQueryScalar_ReturnsBadRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync(
            "/api/enrollments?schoolId=abc&gradeId=1&academicYearId=1", cancellationToken);
        await AssertInvalidRequestAsync(response, cancellationToken);
    }

    [Fact]
    [Trait("Evidence", "IT-PROBLEMS")]
    public async Task UnparseableRouteScalar_ReturnsBadRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/api/schools/abc/teachers", cancellationToken);
        await AssertInvalidRequestAsync(response, cancellationToken);
    }

    [Fact]
    [Trait("Evidence", "IT-PROBLEMS")]
    public async Task UnparseableDateQueryScalar_ReturnsBadRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/api/schools/1/teachers?asOfDate=not-a-date", cancellationToken);
        await AssertInvalidRequestAsync(response, cancellationToken);
    }

    [Fact]
    [Trait("Evidence", "IT-PROBLEMS")]
    public async Task MalformedJsonBody_ReturnsBadRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var content = new StringContent("{ this is not valid json", Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/api/teachers/5/contracts", content, cancellationToken);
        await AssertInvalidRequestAsync(response, cancellationToken);
        Assert.Equal(0, await _context.TeacherContracts.CountAsync(cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-PROBLEMS")]
    public async Task TypeMismatchedBodyField_ReturnsBadRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var content = new StringContent(
            """{"schoolIds":[1],"startDate":"not-a-date"}""", Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/api/teachers/5/contracts", content, cancellationToken);
        await AssertInvalidRequestAsync(response, cancellationToken);
        Assert.Equal(0, await _context.TeacherContracts.CountAsync(cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-PROBLEMS")]
    public async Task EmptyPostBody_ReturnsBadRequest()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/api/teachers/5/contracts", content, cancellationToken);
        await AssertInvalidRequestAsync(response, cancellationToken);
        Assert.Equal(0, await _context.TeacherContracts.CountAsync(cancellationToken));
    }

    private static async Task AssertInvalidRequestAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("invalid_request", root.GetProperty("code").GetString());
        Assert.True(root.TryGetProperty("title", out _));
        Assert.True(root.TryGetProperty("type", out _));
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitApiInvalid_{Guid.NewGuid():N}",
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
