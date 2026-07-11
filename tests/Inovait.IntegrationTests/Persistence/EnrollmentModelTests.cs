using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.People;
using Inovait.Core.Features.Enrollments;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Features.Enrollments;
using Inovait.Infrastructure.Persistence;
using Inovait.Infrastructure.Text;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Inovait.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P0")]
public sealed class EnrollmentModelTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    [Fact]
    public async Task EnrollmentCommand_CreatesReusesAndRejectsADuplicateAnnualEnrollment()
    {
        var firstGroup = new ClassGroup(1, 1, 1, "WF-A");
        var laterYear = new AcademicYear("AY-WF", "Workflow Year", new(2028, 1, 1), new(2028, 12, 31));
        _context.AddRange(firstGroup, laterYear);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var laterGroup = new ClassGroup(1, laterYear.Id, 1, "WF-B");
        _context.Add(laterGroup);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var handler = _scope.ServiceProvider.GetRequiredService<CreateEnrollmentHandler>();
        var created = await handler.HandleAsync(Command(firstGroup.Id, 1), TestContext.Current.CancellationToken);
        var duplicate = await handler.HandleAsync(Command(firstGroup.Id, 1), TestContext.Current.CancellationToken);
        var reused = await handler.HandleAsync(Command(laterGroup.Id, laterYear.Id), TestContext.Current.CancellationToken);
        Assert.True(created.IsSuccess);
        Assert.Equal(EnrollmentError.AnnualEnrollmentConflict, duplicate.Error);
        Assert.True(reused.IsSuccess && reused.StudentReused);
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.Equal((1, 1, 2), (await _context.People.CountAsync(cancellationToken),
            await _context.Students.CountAsync(cancellationToken), await _context.Enrollments.CountAsync(cancellationToken)));
    }
    [Fact]
    public async Task EnrollmentTransaction_CancellationRollsBackAndClearsTracking()
    {
        using var source = new CancellationTokenSource();
        var probe = new RetryProbe(1, false);
        var transaction = new EfEnrollmentWorkflow(
            _context, probe.BeforeAttemptAsync, probe.RecordAttempt);
        IEnrollmentRepository repository = transaction;
        var personSaved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask<EnrollmentResult> CancelAfterPerson(CancellationToken cancellationToken)
        {
            await repository.CreatePersonAsync(new(IdentityResolutionStatus.NewPerson, null, 1, "ROLLBACK-1",
                "Rollback", "Student", new(2012, 1, 1), true), cancellationToken);
            personSaved.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return EnrollmentResult.Failure(EnrollmentError.ConcurrencyConflict);
        }
        var operation = transaction.ExecuteAsync(CancelAfterPerson, source.Token).AsTask();
        await personSaved.Task.WaitAsync(TestContext.Current.CancellationToken);
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal((0, 0), (await _context.People.CountAsync(TestContext.Current.CancellationToken),
            _context.ChangeTracker.Entries().Count()));
        Assert.False(probe.RollbackToken!.Value.CanBeCanceled);
    }
    [Fact]
    public async Task EnrollmentCommand_ConcurrentRequestsCommitExactlyOneAnnualEnrollment()
    {
        var group = new ClassGroup(1, 1, 1, "WF-RACE");
        _context.Add(group);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var firstScope = _provider.CreateAsyncScope();
        await using var secondScope = _provider.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<InovaitDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<InovaitDbContext>();
        var probe = new RetryProbe(2, false);
        var command = Command(group.Id, 1, "RACE-1");
        var first = CreateHandler(firstContext, probe).HandleAsync(command, TestContext.Current.CancellationToken).AsTask();
        var second = CreateHandler(secondContext, probe).HandleAsync(command, TestContext.Current.CancellationToken).AsTask();
        EnrollmentResult[] results = [await first, await second];
        Assert.NotSame(firstContext.Database.GetDbConnection(), secondContext.Database.GetDbConnection());
        Assert.True(probe.Retries > 0);
        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.Error == EnrollmentError.AnnualEnrollmentConflict);
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.Equal((1, 1, 1), (await _context.People.CountAsync(cancellationToken),
            await _context.Students.CountAsync(cancellationToken), await _context.Enrollments.CountAsync(cancellationToken)));
        var exhaustionProbe = new RetryProbe(0, true);
        var exhausted = await CreateHandler(_context, exhaustionProbe)
            .HandleAsync(Command(1, 1), TestContext.Current.CancellationToken);
        Assert.Equal(EnrollmentError.ConcurrencyConflict, exhausted.Error);
        Assert.Equal(3, exhaustionProbe.Retries);
        Assert.Empty(_context.ChangeTracker.Entries());
    }
    [Fact]
    [Trait("Evidence", "IT-ENR-ANNUAL")]
    public async Task Enrollment_AllowsHistoryAcrossYearsButRejectsDuplicateAndDivergentYears()
    {
        var firstStudentId = await AddStudentAsync("ENR-01");
        var secondStudentId = await AddStudentAsync("ENR-02");
        var firstGroup = new ClassGroup(1, 1, 1, " A\t 01 ");
        var secondGroup = new ClassGroup(1, 1, 1, "A02");
        var laterYear = new AcademicYear("AY-2027", "Academic Year 2027", new(2027, 1, 1), new(2027, 12, 31));
        _context.AddRange(firstGroup, secondGroup, laterYear);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var laterGroup = new ClassGroup(1, laterYear.Id, 1, "A01");
        _context.Add(laterGroup);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _context.AddRange(
            new Enrollment(firstStudentId, firstGroup.Id, 1),
            new Enrollment(firstStudentId, laterGroup.Id, laterYear.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, await _context.Enrollments.CountAsync(TestContext.Current.CancellationToken));

        _context.Add(new Enrollment(firstStudentId, secondGroup.Id, 1));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.True(Assert.IsType<SqlException>(duplicate.InnerException).Number is 2601 or 2627);
        _context.ChangeTracker.Clear();

        var divergent = await Assert.ThrowsAsync<SqlException>(() => _context.Database.ExecuteSqlAsync(
            $"INSERT [academic].[Enrollment] ([StudentPersonId],[ClassGroupId],[AcademicYearId]) VALUES ({secondStudentId},{firstGroup.Id},{laterYear.Id})",
            TestContext.Current.CancellationToken));
        Assert.Equal(547, divergent.Number);
        Assert.Equal(2, await _context.Enrollments.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-NORMAL-FORMS")]
    public async Task AcademicFacts_KeepContextNormalizedAndApplyOnlyTheirAuditPolicies()
    {
        var group = new ClassGroup(1, 1, 1, " B\t  01 ");
        _context.Add(group);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal("B 01", group.Code);
        Assert.All([group.CreatedAtUtc != default, group.UpdatedAtUtc != default, group.RowVersion.Length > 0], Assert.True);

        var enrollment = new Enrollment(await AddStudentAsync("NF-01"), group.Id, 1);
        _context.Add(enrollment);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(default, enrollment.CreatedAtUtc);

        var enrollmentType = Model.FindEntityType(typeof(Enrollment))!;
        Assert.Equal(("academic", "Enrollment"), (enrollmentType.GetSchema(), enrollmentType.GetTableName()));
        Assert.Equal(["AcademicYearId", "ClassGroupId", "CreatedAtUtc", "Id", "StudentPersonId"],
            enrollmentType.GetProperties().Select(property => property.Name).Order());
        Assert.Null(enrollmentType.FindProperty("SchoolId"));
        Assert.Null(enrollmentType.FindProperty("GradeId"));
        Assert.Null(enrollmentType.FindProperty("UpdatedAtUtc"));
        Assert.Null(enrollmentType.FindProperty("RowVersion"));
        Assert.Equal("datetime2(3)", enrollmentType.FindProperty(nameof(Enrollment.CreatedAtUtc))!.GetColumnType());
        Assert.Equal("SYSUTCDATETIME()", enrollmentType.FindProperty(nameof(Enrollment.CreatedAtUtc))!.GetDefaultValueSql());

        var enrollmentForeignKeys = enrollmentType.GetForeignKeys().OrderBy(foreignKey => foreignKey.GetConstraintName()).ToArray();
        Assert.Equal(["FK_Enrollment_ClassGroupId_AcademicYearId", "FK_Enrollment_Student"],
            enrollmentForeignKeys.Select(foreignKey => foreignKey.GetConstraintName()));
        Assert.Equal([nameof(Enrollment.ClassGroupId), nameof(Enrollment.AcademicYearId)],
            enrollmentForeignKeys[0].Properties.Select(property => property.Name));
        Assert.Equal([nameof(ClassGroup.Id), nameof(ClassGroup.AcademicYearId)],
            enrollmentForeignKeys[0].PrincipalKey.Properties.Select(property => property.Name));
        Assert.All(enrollmentForeignKeys, foreignKey => Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior));

        var groupType = Model.FindEntityType(typeof(ClassGroup))!;
        Assert.Equal(("academic", "ClassGroup"), (groupType.GetSchema(), groupType.GetTableName()));
        Assert.Equal("varchar(20)", groupType.FindProperty(nameof(ClassGroup.Code))!.GetColumnType());
        Assert.Equal("Latin1_General_100_CI_AS", groupType.FindProperty(nameof(ClassGroup.Code))!.GetCollation());
        Assert.Equal(["CK_ClassGroup_Code_NotBlank", "CK_ClassGroup_UpdatedAtUtc"],
            groupType.GetCheckConstraints().Select(check => check.Name).Order());
        Assert.Equal(["FK_ClassGroup_AcademicYear", "FK_ClassGroup_Grade", "FK_ClassGroup_School"],
            groupType.GetForeignKeys().Select(foreignKey => foreignKey.GetConstraintName()).Order());
        Assert.All(groupType.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior));
        var supportKey = Assert.Single(groupType.GetKeys(), key => !key.IsPrimaryKey());
        Assert.Equal("UQ_ClassGroup_Id_AcademicYear_ForEnrollment", supportKey.GetName());
        Assert.Equal([nameof(ClassGroup.Id), nameof(ClassGroup.AcademicYearId)],
            supportKey.Properties.Select(property => property.Name));
        foreach (var timestampName in new[] { nameof(ClassGroup.CreatedAtUtc), nameof(ClassGroup.UpdatedAtUtc) })
        {
            var timestamp = groupType.FindProperty(timestampName)!;
            Assert.Equal("datetime2(3)", timestamp.GetColumnType());
            Assert.Equal("SYSUTCDATETIME()", timestamp.GetDefaultValueSql());
        }
        Assert.Equal("rowversion", groupType.FindProperty(nameof(ClassGroup.RowVersion))!.GetColumnType());

        var blank = await Assert.ThrowsAsync<SqlException>(() => _context.Database.ExecuteSqlRawAsync(
            "INSERT [academic].[ClassGroup] ([SchoolId],[AcademicYearId],[GradeId],[Code]) VALUES (1,1,1,'   ')",
            TestContext.Current.CancellationToken));
        Assert.Equal(547, blank.Number);
    }

    [Fact]
    public async Task AcademicIndexes_UseCanonicalKeysIncludesAndClusteredIdAvailability()
    {
        AssertIndex<ClassGroup>("IX_ClassGroup_AcademicYearId_GradeId_SchoolId",
            [nameof(ClassGroup.AcademicYearId), nameof(ClassGroup.GradeId), nameof(ClassGroup.SchoolId)], [nameof(ClassGroup.Code)]);
        AssertIndex<ClassGroup>("IX_ClassGroup_GradeId", [nameof(ClassGroup.GradeId)], []);
        var context = AssertIndex<ClassGroup>("UQ_ClassGroup_Context",
            [nameof(ClassGroup.SchoolId), nameof(ClassGroup.AcademicYearId), nameof(ClassGroup.GradeId), nameof(ClassGroup.Code)], []);
        Assert.True(context.IsUnique);
        AssertIndex<Enrollment>("IX_Enrollment_ClassGroupId_StudentPersonId",
            [nameof(Enrollment.ClassGroupId), nameof(Enrollment.StudentPersonId)],
            [nameof(Enrollment.AcademicYearId), nameof(Enrollment.CreatedAtUtc)]);
        var annual = AssertIndex<Enrollment>("UQ_Enrollment_StudentPersonId_AcademicYearId",
            [nameof(Enrollment.StudentPersonId), nameof(Enrollment.AcademicYearId)], []);
        Assert.True(annual.IsUnique);

        foreach (var type in new[] { typeof(ClassGroup), typeof(Enrollment) })
        {
            var entity = Model.FindEntityType(type)!;
            Assert.DoesNotContain(entity.GetIndexes().SelectMany(index => index.GetIncludeProperties() ?? []),
                property => property == "Id");
        }

        var clusteredPrimaryKeys = await _context.Database.SqlQueryRaw<string>(
            "SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name]) AS [Value] FROM sys.tables t JOIN sys.indexes i ON i.[object_id]=t.[object_id] WHERE i.[is_primary_key]=1 AND i.[type]=1 AND t.[name] IN ('ClassGroup','Enrollment') ORDER BY t.[name]")
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["academic.ClassGroup", "academic.Enrollment"], clusteredPrimaryKeys);
    }

    public async ValueTask InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitS05_{Guid.NewGuid():N}",
        }.ConnectionString;
        _provider = new ServiceCollection().AddInovaitInfrastructure(connection).BuildServiceProvider(true);
        _scope = _provider.CreateAsyncScope();
        _context = _scope.ServiceProvider.GetRequiredService<InovaitDbContext>();
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    private IModel Model => _context.GetService<IDesignTimeModel>().Model;

    private async Task<int> AddStudentAsync(string documentNumber)
    {
        var person = new Person(1, documentNumber, "Test", "Student", new(2012, 1, 1));
        _context.Add(person);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.Add(new Student(person.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return person.Id;
    }

    private static CreateEnrollmentCommand Command(int classGroupId, int academicYearId, string document = "WF-1") =>
        new(new("CC", document, "Ada", "Lovelace", new(2012, 1, 1)), 1, academicYearId, 1, classGroupId);

    private static CreateEnrollmentHandler CreateHandler(InovaitDbContext context, RetryProbe probe)
    {
        var workflow = new EfEnrollmentWorkflow(
            context, probe.BeforeAttemptAsync, probe.RecordAttempt);
        return new(new(new TextNormalizer(), workflow, TimeProvider.System), workflow, workflow);
    }

    private sealed class RetryProbe(int participants, bool fail)
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public int Retries;
        public CancellationToken? RollbackToken;
        public async ValueTask<bool> BeforeAttemptAsync(int attempt, CancellationToken cancellationToken)
        {
            if (fail || attempt != 1)
                return fail;
            if (Interlocked.Increment(ref _arrivals) == participants)
                _release.SetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return false;
        }
        public void RecordAttempt(int attempt, CancellationToken token) { if (attempt == 0) RollbackToken = token; else Interlocked.Increment(ref Retries); }
    }

    private IReadOnlyIndex AssertIndex<TEntity>(string name, string[] keys, string[] includes) where TEntity : class
    {
        var index = Assert.Single(Model.FindEntityType(typeof(TEntity))!.GetIndexes(),
            candidate => candidate.GetDatabaseName() == name);
        Assert.Equal(keys, index.Properties.Select(property => property.Name));
        Assert.Equal(includes, index.GetIncludeProperties() ?? []);
        return index;
    }
}
