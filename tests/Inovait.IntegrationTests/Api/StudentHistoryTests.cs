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
[Trait("Priority", "P1")]
public sealed class StudentHistoryTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    [Trait("Evidence", "IT-HISTORY")]
    public async Task GetStudentHistory_OrdersYearsAndAssignmentsAndHandlesDualPersonaAndEmptyEnrollments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Two academic years sharing the same startDate exercise the enrollmentId-ascending tie-break.
        var tieYearA = new AcademicYear("AY-TIE-A", "Tie Year A", new(2025, 1, 1), new(2025, 6, 30));
        var tieYearB = new AcademicYear("AY-TIE-B", "Tie Year B", new(2025, 1, 1), new(2025, 12, 31));
        var laterYear = new AcademicYear("AY-LATER", "Later Year", new(2027, 1, 1), new(2027, 12, 31));
        _context.AddRange(tieYearA, tieYearB, laterYear);
        await _context.SaveChangesAsync(cancellationToken);

        var groupTieA = new ClassGroup(1, tieYearA.Id, 1, "G-TIE-A");
        var groupTieB = new ClassGroup(1, tieYearB.Id, 1, "G-TIE-B");
        var groupLater = new ClassGroup(1, laterYear.Id, 1, "G-LATER");
        var groupCurrent = new ClassGroup(1, 1, 1, "G-CURRENT");
        _context.AddRange(groupTieA, groupTieB, groupLater, groupCurrent);
        await _context.SaveChangesAsync(cancellationToken);

        // Dual persona: the student under test also holds a Teacher role, proving it does not block their own history.
        var studentPersonId = await AddPersonWithRolesAsync(
            "70.100.100", "Ana", "Solis", isStudent: true, isTeacher: true, cancellationToken);
        _context.Add(new Enrollment(studentPersonId, groupTieA.Id, tieYearA.Id));
        await _context.SaveChangesAsync(cancellationToken);
        _context.Add(new Enrollment(studentPersonId, groupTieB.Id, tieYearB.Id));
        await _context.SaveChangesAsync(cancellationToken);
        _context.Add(new Enrollment(studentPersonId, groupLater.Id, laterYear.Id));
        await _context.SaveChangesAsync(cancellationToken);
        // AcademicYear Id=1 ("Academic Year 2026") is production-seeded and the configured current year.
        _context.Add(new Enrollment(studentPersonId, groupCurrent.Id, 1));
        await _context.SaveChangesAsync(cancellationToken);

        var subjectMath = new Subject("SUB-MATH", "Math");
        var subjectHistory = new Subject("SUB-HIST", "History");
        _context.AddRange(subjectMath, subjectHistory);
        await _context.SaveChangesAsync(cancellationToken);

        var teacherZeta = await AddPersonWithRolesAsync(
            "70.900.001", "Wendy", "Zeta", isStudent: false, isTeacher: true, cancellationToken);
        var teacherAlpha = await AddPersonWithRolesAsync(
            "70.900.002", "Beto", "Alpha", isStudent: false, isTeacher: true, cancellationToken);
        var teacherMid = await AddPersonWithRolesAsync(
            "70.900.003", "Carla", "Mid", isStudent: false, isTeacher: true, cancellationToken);

        var contractZeta = await AddContractAsync(teacherZeta, 1, new(2024, 1, 1), null, cancellationToken);
        var contractAlpha = await AddContractAsync(teacherAlpha, 1, new(2024, 1, 1), null, cancellationToken);
        var contractMid = await AddContractAsync(teacherMid, 1, new(2024, 1, 1), null, cancellationToken);

        // Same subject "Math" taught by two teachers on the same class group exercises the
        // teacher.lastNames tie-break after subject.name; weekdays are inserted unsorted.
        var mathZetaAssignmentId = await AddAssignmentAsync(
            contractZeta, groupTieA.Id, subjectMath.Id, tieYearA.StartDate, tieYearA.EndDate, [5, 1, 3], cancellationToken);
        var mathAlphaAssignmentId = await AddAssignmentAsync(
            contractAlpha, groupTieA.Id, subjectMath.Id, tieYearA.StartDate, tieYearA.EndDate, [2], cancellationToken);
        var historyMidAssignmentId = await AddAssignmentAsync(
            contractMid, groupTieA.Id, subjectHistory.Id, tieYearA.StartDate, tieYearA.EndDate, [4], cancellationToken);

        var response = await GetAsync<StudentHistoryResponse>("/api/students/CC/70.100.100/history", cancellationToken);

        Assert.Equal((studentPersonId, "CC", "70.100.100", "Ana", "Solis"),
            (response.StudentId, response.DocumentType, response.DocumentNumber, response.FirstNames, response.LastNames));
        Assert.Equal(4, response.Enrollments.Count);

        // Descending by academicYear.startDate: 2027, 2026 (seeded current), then the tied 2025 pair
        // ordered by enrollmentId ascending (tieYearA enrolled before tieYearB).
        Assert.Equal(
            [laterYear.Id, 1, tieYearA.Id, tieYearB.Id],
            response.Enrollments.Select(item => item.AcademicYear.Id));
        Assert.True(response.Enrollments.Single(item => item.AcademicYear.Id == 1).AcademicYear.IsCurrent);
        Assert.All(
            response.Enrollments.Where(item => item.AcademicYear.Id != 1),
            item => Assert.False(item.AcademicYear.IsCurrent));

        var currentYearItem = response.Enrollments.Single(item => item.AcademicYear.Id == 1);
        Assert.Empty(currentYearItem.TeachingAssignments);

        var tieBItem = response.Enrollments.Single(item => item.AcademicYear.Id == tieYearB.Id);
        Assert.Empty(tieBItem.TeachingAssignments);

        var tieAItem = response.Enrollments.Single(item => item.AcademicYear.Id == tieYearA.Id);
        Assert.Equal(
            [historyMidAssignmentId, mathAlphaAssignmentId, mathZetaAssignmentId],
            tieAItem.TeachingAssignments.Select(assignment => assignment.AssignmentId));
        Assert.Equal(["History", "Math", "Math"], tieAItem.TeachingAssignments.Select(a => a.Subject.Name));
        Assert.Equal(["Mid", "Alpha", "Zeta"], tieAItem.TeachingAssignments.Select(a => a.Teacher.LastNames));
        Assert.Equal(
            [1, 3, 5],
            tieAItem.TeachingAssignments.Single(a => a.AssignmentId == mathZetaAssignmentId).Weekdays);

        using var rawResponse = await _client.GetAsync("/api/students/CC/70.100.100/history", cancellationToken);
        var rawJson = await rawResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        var root = rawJson!.RootElement;
        Assert.Equal(
            new[] { "studentId", "documentType", "documentNumber", "firstNames", "lastNames", "birthDate", "enrollments" },
            root.EnumerateObject().Select(property => property.Name));
        var rawTieAEnrollment = root.GetProperty("enrollments").EnumerateArray()
            .Single(item => item.GetProperty("academicYear").GetProperty("id").GetInt32() == tieYearA.Id);
        Assert.Equal(
            new[] { "enrollmentId", "academicYear", "school", "grade", "classGroup", "teachingAssignments" },
            rawTieAEnrollment.EnumerateObject().Select(property => property.Name));
        var rawFirstAssignment = rawTieAEnrollment.GetProperty("teachingAssignments")[0];
        Assert.Equal(
            new[] { "assignmentId", "teacher", "subject", "weekdays" },
            rawFirstAssignment.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    [Trait("Evidence", "IT-HISTORY")]
    public async Task GetStudentHistory_StudentWithoutEnrollmentsReturnsEmptyArray()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var personId = await AddPersonWithRolesAsync(
            "70.400.400", "Nina", "Solo", isStudent: true, isTeacher: false, cancellationToken);

        var response = await GetAsync<StudentHistoryResponse>("/api/students/CC/70.400.400/history", cancellationToken);

        Assert.Equal(personId, response.StudentId);
        Assert.Empty(response.Enrollments);
    }

    [Fact]
    [Trait("Evidence", "IT-HISTORY")]
    public async Task GetStudentHistory_UnknownOrNonStudentDocument_ReturnsNotFoundWithProblemDetailsShape()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddPersonWithRolesAsync("70.500.500", "Teacher", "Only", isStudent: false, isTeacher: true, cancellationToken);

        using var missingResponse = await _client.GetAsync("/api/students/CC/99.999.999/history", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal("application/problem+json", missingResponse.Content.Headers.ContentType?.MediaType);
        var missingProblem = await missingResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal(404, missingProblem!.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("student_not_found", missingProblem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "https://inovait.local/problems/student-not-found", missingProblem.RootElement.GetProperty("type").GetString());
        Assert.True(missingProblem.RootElement.TryGetProperty("title", out _));

        // A person that only holds the Teacher role must not resolve as a student.
        using var teacherOnlyResponse = await _client.GetAsync("/api/students/CC/70.500.500/history", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, teacherOnlyResponse.StatusCode);
        var teacherOnlyProblem = await teacherOnlyResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.Equal("student_not_found", teacherOnlyProblem!.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    [Trait("Evidence", "IT-HISTORY")]
    public async Task GetStudentHistory_NormalizesDocumentTypeCaseAndInternalWhitespaceBeforeLookup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var personId = await AddPersonWithRolesAsync(
            "70 600 600", "Rio", "Norma", isStudent: true, isTeacher: false, cancellationToken);

        var response = await GetAsync<StudentHistoryResponse>(
            $"/api/students/{Uri.EscapeDataString("cc")}/{Uri.EscapeDataString("70   600   600")}/history", cancellationToken);

        Assert.Equal(personId, response.StudentId);
        Assert.Equal("70 600 600", response.DocumentNumber);
    }

    private async Task<int> AddPersonWithRolesAsync(
        string documentNumber, string firstNames, string lastNames, bool isStudent, bool isTeacher,
        CancellationToken cancellationToken)
    {
        var person = new Person(1, documentNumber, firstNames, lastNames, new DateOnly(1990, 1, 1));
        _context.Add(person);
        await _context.SaveChangesAsync(cancellationToken);
        if (isStudent)
        {
            _context.Add(new Student(person.Id));
        }

        if (isTeacher)
        {
            _context.Add(new Teacher(person.Id));
        }

        if (isStudent || isTeacher)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return person.Id;
    }

    private async Task<int> AddContractAsync(
        int teacherPersonId, int schoolId, DateOnly startDate, DateOnly? endDate, CancellationToken cancellationToken)
    {
        var contract = new TeacherContract(teacherPersonId, schoolId, startDate, endDate);
        _context.Add(contract);
        await _context.SaveChangesAsync(cancellationToken);
        return contract.Id;
    }

    private async Task<int> AddAssignmentAsync(
        int teacherContractId, int classGroupId, int subjectId, DateOnly startDate, DateOnly? endDate,
        byte[] weekdays, CancellationToken cancellationToken)
    {
        var assignment = new TeachingAssignment(teacherContractId, classGroupId, subjectId, startDate, endDate);
        _context.Add(assignment);
        await _context.SaveChangesAsync(cancellationToken);
        foreach (var weekday in weekdays)
        {
            _context.Add(new ClassSchedule(assignment.Id, weekday));
        }

        await _context.SaveChangesAsync(cancellationToken);
        return assignment.Id;
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
            InitialCatalog = $"InovaitApiHistory_{Guid.NewGuid():N}",
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
