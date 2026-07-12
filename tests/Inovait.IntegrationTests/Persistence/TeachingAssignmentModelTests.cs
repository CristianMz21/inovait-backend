using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.People;
using Inovait.Core.Domain.Staff;
using Inovait.Core.Features.TeachingAssignments;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Persistence;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inovait.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P1")]
public sealed class TeachingAssignmentModelTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    private int _teacherContractId;
    private int _classGroupId;
    private int _otherSchoolGroupId;
    private int _subjectId;

    [Fact]
    [Trait("Evidence", "IT-ASSIGNMENT-PERIOD")]
    public async Task InPeriodAssignment_SucceedsAndPersistsScheduleAtomically()
    {
        var handler = _scope.ServiceProvider.GetRequiredService<CreateTeachingAssignmentHandler>();

        var result = await handler.HandleAsync(
            new CreateTeachingAssignmentCommand(_teacherContractId, _subjectId, _classGroupId,
                new(2026, 3, 1), new(2026, 11, 30), [1, 3]),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await _context.TeachingAssignments.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal([(byte)1, (byte)3], await _context.ClassSchedules
            .Where(schedule => schedule.TeachingAssignmentId == result.AssignmentId)
            .Select(schedule => schedule.Weekday).OrderBy(weekday => weekday)
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [Trait("Evidence", "IT-ASSIGNMENT-PERIOD")]
    [InlineData("2025-12-01", "2026-06-30")]
    [InlineData("2026-03-01", "2027-01-31")]
    public async Task OutOfPeriodAssignment_RejectsTransactionallyWithoutPartialRows(string start, string end)
    {
        var handler = _scope.ServiceProvider.GetRequiredService<CreateTeachingAssignmentHandler>();

        var result = await handler.HandleAsync(
            new CreateTeachingAssignmentCommand(_teacherContractId, _subjectId, _classGroupId,
                DateOnly.Parse(start), DateOnly.Parse(end), [1]),
            TestContext.Current.CancellationToken);

        Assert.Equal(TeachingAssignmentError.PeriodNotContained, result.Error);
        Assert.Equal(0, await _context.TeachingAssignments.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await _context.ClassSchedules.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-ASSIGNMENT-PERIOD")]
    public async Task DifferentSchoolAssignment_RejectsTransactionallyWithoutPartialRows()
    {
        var handler = _scope.ServiceProvider.GetRequiredService<CreateTeachingAssignmentHandler>();

        var result = await handler.HandleAsync(
            new CreateTeachingAssignmentCommand(_teacherContractId, _subjectId, _otherSchoolGroupId,
                new(2026, 3, 1), new(2026, 11, 30), [1]),
            TestContext.Current.CancellationToken);

        Assert.Equal(TeachingAssignmentError.SchoolMismatch, result.Error);
        Assert.Equal(0, await _context.TeachingAssignments.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await _context.ClassSchedules.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-ASSIGNMENT-PERIOD")]
    public async Task CancelledContractAssignment_RejectsPeriodPastCancellationWithoutPartialRows()
    {
        var contract = await _context.TeacherContracts.SingleAsync(
            candidate => candidate.Id == _teacherContractId, TestContext.Current.CancellationToken);
        contract.Cancel(new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), "Position closed", new(2026, 5, 31));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
        var handler = _scope.ServiceProvider.GetRequiredService<CreateTeachingAssignmentHandler>();

        var result = await handler.HandleAsync(
            new CreateTeachingAssignmentCommand(_teacherContractId, _subjectId, _classGroupId,
                new(2026, 6, 1), new(2026, 11, 30), [1]),
            TestContext.Current.CancellationToken);

        Assert.Equal(TeachingAssignmentError.PeriodNotContained, result.Error);
        Assert.Equal(0, await _context.TeachingAssignments.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-ASSIGNMENT-PERIOD")]
    public async Task Mapping_DeclaresDateRangeAndWeekdayChecks()
    {
        await AssertCheckAsync($"INSERT [academic].[TeachingAssignment] ([TeacherContractId],[ClassGroupId],[SubjectId],[StartDate],[EndDate]) VALUES ({_teacherContractId},{_classGroupId},{_subjectId},'2026-06-01','2026-01-01')");

        var handler = _scope.ServiceProvider.GetRequiredService<CreateTeachingAssignmentHandler>();
        var created = await handler.HandleAsync(
            new CreateTeachingAssignmentCommand(_teacherContractId, _subjectId, _classGroupId,
                new(2026, 3, 1), new(2026, 11, 30), [1]),
            TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess);

        await AssertCheckAsync($"INSERT [academic].[ClassSchedule] ([TeachingAssignmentId],[Weekday]) VALUES ({created.AssignmentId},8)");
    }

    public async ValueTask InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitS13A_{Guid.NewGuid():N}",
        }.ConnectionString;
        _provider = new ServiceCollection().AddInovaitInfrastructure(connection).BuildServiceProvider(true);
        _scope = _provider.CreateAsyncScope();
        _context = _scope.ServiceProvider.GetRequiredService<InovaitDbContext>();
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedBaselineAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    private async Task SeedBaselineAsync()
    {
        var person = new Person(1, $"TA-{Guid.NewGuid():N}"[..20], "Ana", "Docente", new(1985, 1, 1));
        _context.Add(person);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var teacher = new Teacher(person.Id);
        var group = new ClassGroup(1, 1, 1, $"G-{Guid.NewGuid():N}"[..20]);
        var otherSchool = new School($"SCH-{Guid.NewGuid():N}"[..10], "Other School", SchoolSector.Private);
        var subject = new Subject($"SUB-{Guid.NewGuid():N}"[..10], "Mathematics");
        _context.AddRange(teacher, group, otherSchool, subject);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var otherGroup = new ClassGroup(otherSchool.Id, 1, 1, $"G-{Guid.NewGuid():N}"[..20]);
        var contract = new TeacherContract(teacher.PersonId, 1, new(2026, 1, 1), new(2026, 12, 31));
        _context.AddRange(otherGroup, contract);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _teacherContractId = contract.Id;
        _classGroupId = group.Id;
        _otherSchoolGroupId = otherGroup.Id;
        _subjectId = subject.Id;
    }

    private async Task AssertCheckAsync(string sql)
    {
        var failure = await Assert.ThrowsAsync<SqlException>(() =>
            _context.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken));
        Assert.Equal(547, failure.Number);
    }
}
