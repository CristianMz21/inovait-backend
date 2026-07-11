using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.People;
using Inovait.Core.Domain.Staff;
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
public sealed class TeacherContractModelTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;

    [Fact]
    [Trait("Evidence", "IT-CON-CANCELLATION")]
    public async Task CancellationChecks_RequireCompleteConsistentData()
    {
        var teacherId = await AddTeacherAsync();
        var contract = new TeacherContract(teacherId, 1, new(2026, 3, 1), new(2026, 11, 30));
        _context.Add(contract);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        contract.Cancel(new(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc), " Position\t closed ", new(2026, 7, 31));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal((TeacherContractStatus.Cancelled, "Position closed"), (contract.Status, contract.CancellationReason));
        Assert.All([contract.CreatedAtUtc != default, contract.UpdatedAtUtc != default, contract.RowVersion.Length > 0], Assert.True);

        await AssertCheckAsync($"INSERT [staff].[TeacherContract] ([TeacherPersonId],[SchoolId],[StartDate],[Status],[CancelledAtUtc]) VALUES ({teacherId},1,'2027-01-01','Confirmed',SYSUTCDATETIME())");
        await AssertCheckAsync($"INSERT [staff].[TeacherContract] ([TeacherPersonId],[SchoolId],[StartDate],[Status],[CancelledAtUtc],[CancellationReason],[CancellationEffectiveDate]) VALUES ({teacherId},1,'2028-01-01','Cancelled',SYSUTCDATETIME(),N'   ','2028-01-01')");
        await AssertCheckAsync($"INSERT [staff].[TeacherContract] ([TeacherPersonId],[SchoolId],[StartDate],[EndDate],[Status],[CancelledAtUtc],[CancellationReason],[CancellationEffectiveDate]) VALUES ({teacherId},1,'2029-01-01','2029-06-30','Cancelled',SYSUTCDATETIME(),N'Closed','2029-07-01')");
    }

    [Fact]
    [Trait("ModelEvidence", "CONTRACT-EXACT-OPEN-UNIQUE")]
    public async Task ExactOpenDuplicateIsRejectedWhileAnotherSchoolCanUseTheSamePeriod()
    {
        var teacherId = await AddTeacherAsync();
        var secondSchool = new School("SCH-002", "Second School", SchoolSector.Private);
        _context.AddRange(secondSchool, new TeacherContract(teacherId, 1, new(2026, 3, 1), null));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.Add(new TeacherContract(teacherId, 1, new(2026, 3, 1), null));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.True(Assert.IsType<SqlException>(duplicate.InnerException).Number is 2601 or 2627);
        _context.ChangeTracker.Clear();
        _context.Add(new TeacherContract(teacherId, secondSchool.Id, new(2026, 3, 1), null));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, await _context.TeacherContracts.CountAsync(TestContext.Current.CancellationToken));
        await AssertCheckAsync("INSERT [staff].[TeacherContract] ([TeacherPersonId],[SchoolId],[StartDate],[Status]) VALUES (999999,1,'2030-01-01','Confirmed')");
        await AssertCheckAsync($"INSERT [staff].[TeacherContract] ([TeacherPersonId],[SchoolId],[StartDate],[Status]) VALUES ({teacherId},999999,'2030-01-01','Confirmed')");
    }

    [Fact]
    public async Task Mapping_UsesCanonicalKeysChecksForeignKeysIndexesAndAudit()
    {
        var entity = Model.FindEntityType(typeof(TeacherContract))!;
        Assert.Equal(("staff", "TeacherContract", "PK_TeacherContract"),
            (entity.GetSchema(), entity.GetTableName(), entity.FindPrimaryKey()!.GetName()));
        Assert.Equal(["CK_TeacherContract_CancellationEffectiveDate", "CK_TeacherContract_CancellationReason_NotBlank",
            "CK_TeacherContract_DateRange", "CK_TeacherContract_Status", "CK_TeacherContract_Status_NotBlank",
            "CK_TeacherContract_StatusCancellation", "CK_TeacherContract_UpdatedAtUtc"],
            entity.GetCheckConstraints().Select(check => check.Name).Order());
        Assert.Equal(["FK_TeacherContract_School", "FK_TeacherContract_Teacher"],
            entity.GetForeignKeys().Select(key => key.GetConstraintName()).Order());
        Assert.All(entity.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.NoAction, key.DeleteBehavior));
        Assert.Equal("varchar(10)", entity.FindProperty(nameof(TeacherContract.Status))!.GetColumnType());
        Assert.Equal("nvarchar(300)", entity.FindProperty(nameof(TeacherContract.CancellationReason))!.GetColumnType());
        Assert.Equal("rowversion", entity.FindProperty(nameof(TeacherContract.RowVersion))!.GetColumnType());
        foreach (var name in new[] { nameof(TeacherContract.CreatedAtUtc), nameof(TeacherContract.UpdatedAtUtc) })
        {
            Assert.Equal(("datetime2(3)", "SYSUTCDATETIME()"),
                (entity.FindProperty(name)!.GetColumnType(), entity.FindProperty(name)!.GetDefaultValueSql()));
        }
        var exact = AssertIndex("UQ_TeacherContract_Exact",
            [nameof(TeacherContract.TeacherPersonId), nameof(TeacherContract.SchoolId), nameof(TeacherContract.StartDate), nameof(TeacherContract.EndDate)], []);
        Assert.True(exact.IsUnique);
        Assert.Null(exact.GetFilter());
        AssertIndex("IX_TeacherContract_TeacherPersonId_StartDate_EndDate",
            [nameof(TeacherContract.TeacherPersonId), nameof(TeacherContract.StartDate), nameof(TeacherContract.EndDate)],
            [nameof(TeacherContract.SchoolId), nameof(TeacherContract.Status), nameof(TeacherContract.CancelledAtUtc), nameof(TeacherContract.CancellationReason), nameof(TeacherContract.CancellationEffectiveDate)]);
        AssertIndex("IX_TeacherContract_SchoolId_StartDate_EndDate",
            [nameof(TeacherContract.SchoolId), nameof(TeacherContract.StartDate), nameof(TeacherContract.EndDate)],
            [nameof(TeacherContract.TeacherPersonId), nameof(TeacherContract.Status), nameof(TeacherContract.CancellationEffectiveDate)]);
        Assert.DoesNotContain(entity.GetIndexes().SelectMany(index => index.GetIncludeProperties() ?? []), name => name == "Id");
        var clustered = await _context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM sys.tables t JOIN sys.indexes i ON i.[object_id]=t.[object_id] WHERE SCHEMA_NAME(t.[schema_id])='staff' AND t.[name]='TeacherContract' AND i.[is_primary_key]=1 AND i.[type]=1")
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, clustered);
    }

    public async ValueTask InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = $"InovaitS06_{Guid.NewGuid():N}" }.ConnectionString;
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
    private async Task<int> AddTeacherAsync()
    {
        var person = new Person(1, $"T-{Guid.NewGuid():N}"[..22], "Test", "Teacher", new(1990, 1, 1));
        _context.Add(person);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.Add(new Teacher(person.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return person.Id;
    }
    private async Task AssertCheckAsync(string sql)
    {
        var failure = await Assert.ThrowsAsync<SqlException>(() =>
            _context.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken));
        Assert.Equal(547, failure.Number);
    }
    private IReadOnlyIndex AssertIndex(string name, string[] keys, string[] includes)
    {
        var index = Assert.Single(Model.FindEntityType(typeof(TeacherContract))!.GetIndexes(),
            candidate => candidate.GetDatabaseName() == name);
        Assert.Equal(keys, index.Properties.Select(property => property.Name));
        Assert.Equal(includes, index.GetIncludeProperties() ?? []);
        return index;
    }
}
