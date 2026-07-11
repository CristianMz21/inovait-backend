using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.People;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Persistence;
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

    private IReadOnlyIndex AssertIndex<TEntity>(string name, string[] keys, string[] includes) where TEntity : class
    {
        var index = Assert.Single(Model.FindEntityType(typeof(TEntity))!.GetIndexes(),
            candidate => candidate.GetDatabaseName() == name);
        Assert.Equal(keys, index.Properties.Select(property => property.Name));
        Assert.Equal(includes, index.GetIncludeProperties() ?? []);
        return index;
    }
}
