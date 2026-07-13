using System.Text.RegularExpressions;
using Inovait.Core.Domain.Catalogs;
using Inovait.Infrastructure.Persistence;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Inovait.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P0")]
public sealed partial class P0DatabaseProtectionTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private InovaitDbContext _context = null!;
    [Fact]
    [Trait("Evidence", "IT-SCHEMAS-P0")]
    public async Task MigrationChain_IsIdempotentReversibleAndCreatesExactP0Schema()
    {
        // InitializeAsync already migrated this context up to the last P0 migration.
        Assert.Equal(ExpectedTables, await ReadTablesAsync());
        Assert.Equal(ExpectedMigrations, await _context.Database.GetAppliedMigrationsAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(3, await ScalarAsync("SELECT (SELECT COUNT(*) FROM [catalog].[School] WHERE [Id]=1 AND [CreatedAtUtc]='2026-01-01' AND [UpdatedAtUtc]='2026-01-01')+(SELECT COUNT(*) FROM [catalog].[AcademicYear] WHERE [Id]=1 AND [CreatedAtUtc]='2026-01-01' AND [UpdatedAtUtc]='2026-01-01')+(SELECT COUNT(*) FROM [catalog].[Grade] WHERE [Id]=1 AND [CreatedAtUtc]='2026-01-01' AND [UpdatedAtUtc]='2026-01-01') AS [Value]"));
        var migrator = _context.GetService<IMigrator>();
        // Pinned to the last P0 migration: with the P1 migrations also present in the assembly,
        // an unpinned GenerateScript() would default to "latest" and pull Subject/TeachingAssignment/
        // ClassSchedule into this idempotent script too, inflating ReadTablesAsync() below past 11.
        var script = migrator.GenerateScript(toMigration: ExpectedMigrations[1], options: MigrationsSqlGenerationOptions.Idempotent);
        await ExecuteCommandAsync("DROP ROLE [inovait_runtime]; CREATE ROLE [inovait_runtime]");
        var foreignDown = await Assert.ThrowsAsync<SqlException>(() =>
            migrator.MigrateAsync(ExpectedMigrations[0], TestContext.Current.CancellationToken));
        Assert.Equal(51006, foreignDown.Number);
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) AS [Value] FROM sys.database_principals WHERE [name]=N'inovait_runtime'"));
        await ExecuteCommandAsync("DROP ROLE [inovait_runtime]");
        await migrator.MigrateAsync(ExpectedMigrations[0], TestContext.Current.CancellationToken);
        await migrator.MigrateAsync(ExpectedMigrations[1], TestContext.Current.CancellationToken);
        await ExecuteCommandAsync(
            "CREATE USER [inovait_runtime_rollback_test] WITHOUT LOGIN; ALTER ROLE [inovait_runtime] ADD MEMBER [inovait_runtime_rollback_test]");
        await migrator.MigrateAsync(Migration.InitialDatabase, TestContext.Current.CancellationToken);
        Assert.Empty(await ReadTablesAsync());
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) AS [Value] FROM sys.database_principals WHERE [name]=N'inovait_runtime'"));
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) AS [Value] FROM sys.database_principals WHERE [name]=N'inovait_runtime_rollback_test'"));
        Assert.Equal(0, await ScalarAsync("SELECT COUNT(*) AS [Value] FROM sys.database_role_members membership JOIN sys.database_principals member ON member.[principal_id]=membership.[member_principal_id] WHERE member.[name]=N'inovait_runtime_rollback_test'"));
        await ExecuteCommandAsync("CREATE ROLE [inovait_runtime]");
        var foreignRole = await Assert.ThrowsAsync<SqlException>(() =>
            migrator.MigrateAsync(ExpectedMigrations[1], TestContext.Current.CancellationToken));
        Assert.Equal(51005, foreignRole.Number);
        await ExecuteCommandAsync("DROP ROLE [inovait_runtime]");
        await ExecuteScriptAsync(script);
        await ExecuteScriptAsync(script);
        Assert.Equal(ExpectedTables, await ReadTablesAsync());
        Assert.Equal(ExpectedMigrations, await _context.Database.GetAppliedMigrationsAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-IMMUTABILITY")]
    public async Task P0TriggersAndEfSaveBehavior_ProtectStableValuesOnly()
    {
        var model = _context.GetService<IDesignTimeModel>().Model;
        Assert.Equal(PropertySaveBehavior.Throw, AfterSave<School>(model, nameof(School.Code)));
        Assert.Equal(PropertySaveBehavior.Throw, AfterSave<School>(model, nameof(School.Sector)));
        Assert.Equal(PropertySaveBehavior.Throw, AfterSave<AcademicYear>(model, nameof(AcademicYear.Code)));
        Assert.Equal(PropertySaveBehavior.Throw, AfterSave<Grade>(model, nameof(Grade.Code)));
        var triggers = await _context.Database.SqlQueryRaw<string>(
            "SELECT CONCAT(OBJECT_SCHEMA_NAME(t.[object_id]),'.',t.[name]) AS [Value] FROM sys.triggers t WHERE t.[parent_class]=1 ORDER BY [Value]")
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal([
            "catalog.TR_AcademicConfiguration_PreventDelete", "catalog.TR_AcademicYear_ProtectCode",
            "catalog.TR_Grade_ProtectCode", "catalog.TR_School_ProtectStableValues"], triggers);
        Assert.Equal(3, await _context.Database.ExecuteSqlRawAsync(
            "UPDATE [catalog].[School] SET [Name]=N'Mutable school' WHERE [Id]=1; UPDATE [catalog].[AcademicYear] SET [Name]=N'Mutable year' WHERE [Id]=1; UPDATE [catalog].[Grade] SET [Name]=N'Mutable grade' WHERE [Id]=1",
            TestContext.Current.CancellationToken));
        await AssertSqlNumberAsync("UPDATE [catalog].[School] SET [Code]='sch-001' WHERE [Id]=1", 51001);
        await AssertSqlNumberAsync("UPDATE [catalog].[School] SET [Sector]='public' WHERE [Id]=1", 51001);
        await AssertSqlNumberAsync("UPDATE [catalog].[AcademicYear] SET [Code]='ay-2026' WHERE [Id]=1", 51002);
        await AssertSqlNumberAsync("UPDATE [catalog].[Grade] SET [Code]='g01' WHERE [Id]=1", 51003);
    }

    [Fact]
    [Trait("Evidence", "IT-SINGLETON")]
    public async Task AcademicConfiguration_IsSeededProtectedAndRequiredAtStartup()
    {
        Assert.Equal(1, await _context.AcademicConfigurations.CountAsync(
            value => value.Id == 1 && value.CurrentAcademicYearId == 1, TestContext.Current.CancellationToken));
        await AssertSqlNumberAsync(
            "INSERT [catalog].[AcademicConfiguration] ([Id],[CurrentAcademicYearId]) VALUES (2,1)", 547);
        await AssertSqlNumberAsync("DELETE [catalog].[AcademicConfiguration] WHERE [Id]=1", 51004);
        await new AcademicConfigurationStartupCheck(_context)
            .EnsurePresentAsync(TestContext.Current.CancellationToken);
        await _context.Database.ExecuteSqlRawAsync(
            "DISABLE TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete] ON [catalog].[AcademicConfiguration]; DELETE [catalog].[AcademicConfiguration] WHERE [Id]=1; ENABLE TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete] ON [catalog].[AcademicConfiguration]",
            TestContext.Current.CancellationToken);
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AcademicConfigurationStartupCheck(_context).EnsurePresentAsync(TestContext.Current.CancellationToken));
        Assert.Contains("AcademicConfiguration(Id=1)", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Evidence", "IT-REFERENCE-PERMISSIONS")]
    public async Task RuntimeRole_CanReadReferenceAndUpdateOnlyCurrentYear()
    {
        await _context.Database.ExecuteSqlRawAsync(
            "CREATE USER [inovait_runtime_test] WITHOUT LOGIN; ALTER ROLE [inovait_runtime] ADD MEMBER [inovait_runtime_test]",
            TestContext.Current.CancellationToken);
        var permissions = await _context.Database.SqlQueryRaw<string>(
            "SELECT CONCAT(OBJECT_SCHEMA_NAME(p.[major_id]),'.',OBJECT_NAME(p.[major_id]),':',p.[permission_name],':',p.[state_desc]) AS [Value] FROM sys.database_permissions p WHERE p.[grantee_principal_id]=DATABASE_PRINCIPAL_ID(N'inovait_runtime') AND p.[class]=1 ORDER BY [Value]")
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal([
            "catalog.AcademicConfiguration:DELETE:DENY", "catalog.AcademicConfiguration:INSERT:DENY", "catalog.AcademicConfiguration:SELECT:GRANT", "catalog.AcademicConfiguration:UPDATE:GRANT",
            "catalog.DocumentType:DELETE:DENY", "catalog.DocumentType:INSERT:DENY", "catalog.DocumentType:SELECT:GRANT", "catalog.DocumentType:UPDATE:DENY"], permissions);
        Assert.Equal(ExpectedMigrations[1], await _context.Database.SqlQueryRaw<string>(
            "SELECT CONVERT(nvarchar(128),ep.[value]) AS [Value] FROM sys.extended_properties ep WHERE ep.[class]=4 AND ep.[major_id]=DATABASE_PRINCIPAL_ID(N'inovait_runtime') AND ep.[name]=N'InovaitMigrationOwner'")
            .SingleAsync(TestContext.Current.CancellationToken));
        await ExecuteAsRuntimeAsync("SELECT COUNT_BIG(*) FROM [catalog].[DocumentType]");
        await ExecuteAsRuntimeAsync("UPDATE [catalog].[AcademicConfiguration] SET [CurrentAcademicYearId]=1 WHERE [Id]=1");
        await AssertRuntimeDeniedAsync("INSERT [catalog].[DocumentType] ([Code],[Name],[IsActive]) VALUES ('PP',N'Passport',1)");
        await AssertRuntimeDeniedAsync("UPDATE [catalog].[DocumentType] SET [Name]=N'Blocked' WHERE [Id]=1");
        await AssertRuntimeDeniedAsync("DELETE [catalog].[DocumentType] WHERE [Id]=1");
        await AssertRuntimeDeniedAsync("INSERT [catalog].[AcademicConfiguration] ([Id],[CurrentAcademicYearId]) VALUES (2,1)");
        await AssertRuntimeDeniedAsync("DELETE [catalog].[AcademicConfiguration] WHERE [Id]=1");
    }
    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitS07_{Guid.NewGuid():N}",
        }.ConnectionString;
        _context = new InovaitDbContext(new DbContextOptionsBuilder<InovaitDbContext>()
            .UseSqlServer(connectionString).Options);
        // Pinned to the P0 migration boundary (not "latest") so this frozen S07 evidence
        // (IT-SCHEMAS-P0/IT-IMMUTABILITY/IT-SINGLETON/IT-REFERENCE-PERMISSIONS: exactly 11 tables,
        // 4 triggers, 2 migrations) stays an exact historical snapshot even though the P1 migrations
        // (AddP1TeachingModel, AddP1DatabaseProtections) now also exist in this assembly.
        await _context.GetService<IMigrator>().MigrateAsync(
            ExpectedMigrations[1], TestContext.Current.CancellationToken);
    }
    public async ValueTask DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await _context.DisposeAsync();
    }

    private async Task<string[]> ReadTablesAsync() => await _context.Database.SqlQueryRaw<string>(
        "SELECT CONCAT(SCHEMA_NAME([schema_id]),'.',[name]) AS [Value] FROM sys.tables WHERE [name]<>'__EFMigrationsHistory' ORDER BY [Value]")
        .ToArrayAsync(TestContext.Current.CancellationToken);
    private async Task ExecuteScriptAsync(string script)
    {
        foreach (var batch in SqlBatchSeparatorRegex().Split(script))
        {
            if (!string.IsNullOrWhiteSpace(batch))
                await _context.Database.ExecuteSqlRawAsync(batch, TestContext.Current.CancellationToken);
        }
    }
    private Task ExecuteAsRuntimeAsync(string command) => ExecuteCommandAsync(
        $"BEGIN TRY EXECUTE AS USER=N'inovait_runtime_test'; {command}; REVERT; END TRY BEGIN CATCH IF USER_NAME()=N'inovait_runtime_test' REVERT; THROW; END CATCH");
    private Task AssertRuntimeDeniedAsync(string command) => AssertSqlNumberAsync(
        $"BEGIN TRY EXECUTE AS USER=N'inovait_runtime_test'; {command}; REVERT; END TRY BEGIN CATCH IF USER_NAME()=N'inovait_runtime_test' REVERT; THROW; END CATCH", 229);
    private async Task AssertSqlNumberAsync(string command, int number)
    {
        var exception = await Assert.ThrowsAsync<SqlException>(() => ExecuteCommandAsync(command));
        Assert.Equal(number, exception.Number);
    }
    private async Task ExecuteCommandAsync(string command)
    {
        await _context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var sqlCommand = _context.Database.GetDbConnection().CreateCommand();
        sqlCommand.CommandText = command;
        await sqlCommand.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
    private Task<int> ScalarAsync(string command) => _context.Database.SqlQueryRaw<int>(command)
        .SingleAsync(TestContext.Current.CancellationToken);
    private static PropertySaveBehavior AfterSave<TEntity>(IModel model, string propertyName) where TEntity : class =>
        model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetAfterSaveBehavior();
    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SqlBatchSeparatorRegex();
    private static readonly string[] ExpectedMigrations =
        ["20260711161500_InitialP0ProductionModel", "20260711161518_AddP0DatabaseProtections"];
    private static readonly string[] ExpectedTables = ["academic.ClassGroup", "academic.Enrollment", "catalog.AcademicConfiguration", "catalog.AcademicYear",
        "catalog.DocumentType", "catalog.Grade", "catalog.School", "people.Person", "people.Student", "people.Teacher", "staff.TeacherContract"];
}

// P1 migration-chain verification (V2-T083 deliverable, mirroring V2-T046's IT-SCHEMAS-P0 test):
// on a clean database the full 4-migration chain produces the 14-table P1 universe with all five
// triggers and an unchanged (8-permission) runtime role; Down of AddP1DatabaseProtections drops
// only its own object (TR_Subject_ProtectCode plus, transitively, AddP1TeachingModel's three
// tables), never the inovait_runtime role or any P0 trigger/table; the chain re-applies cleanly.
[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P1")]
public sealed class P1DatabaseProtectionTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private InovaitDbContext _context = null!;

    [Fact]
    [Trait("Evidence", "IT-SQL-SCRIPT-P1")]
    public async Task MigrationChain_AppliesAllFourMigrationsAndReversesOnlyItsOwnP1Objects()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var migrator = _context.GetService<IMigrator>();

        await migrator.MigrateAsync(cancellationToken: cancellationToken);
        Assert.Equal(ExpectedP1Tables, await ReadTablesAsync());
        Assert.Equal(ExpectedP1Migrations, await _context.Database.GetAppliedMigrationsAsync(cancellationToken));
        Assert.Equal(ExpectedP1Triggers, await ReadTriggersAsync());
        Assert.Equal(8, await ScalarAsync(
            "SELECT COUNT(*) AS [Value] FROM sys.database_permissions p WHERE p.[grantee_principal_id]=DATABASE_PRINCIPAL_ID(N'inovait_runtime') AND p.[class]=1"));

        await migrator.MigrateAsync(ExpectedP1Migrations[1], cancellationToken);
        Assert.Equal(ExpectedP0Tables, await ReadTablesAsync());
        Assert.Equal(ExpectedP0Triggers, await ReadTriggersAsync());
        Assert.Equal(1, await ScalarAsync("SELECT COUNT(*) AS [Value] FROM sys.database_principals WHERE [name]=N'inovait_runtime'"));
        Assert.Equal(8, await ScalarAsync(
            "SELECT COUNT(*) AS [Value] FROM sys.database_permissions p WHERE p.[grantee_principal_id]=DATABASE_PRINCIPAL_ID(N'inovait_runtime') AND p.[class]=1"));

        await migrator.MigrateAsync(cancellationToken: cancellationToken);
        Assert.Equal(ExpectedP1Tables, await ReadTablesAsync());
        Assert.Equal(ExpectedP1Triggers, await ReadTriggersAsync());
        Assert.Equal(ExpectedP1Migrations, await _context.Database.GetAppliedMigrationsAsync(cancellationToken));
    }

    public ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitS13B_{Guid.NewGuid():N}",
        }.ConnectionString;
        _context = new InovaitDbContext(new DbContextOptionsBuilder<InovaitDbContext>()
            .UseSqlServer(connectionString).Options);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    private async Task<string[]> ReadTablesAsync() => await _context.Database.SqlQueryRaw<string>(
        "SELECT CONCAT(SCHEMA_NAME([schema_id]),'.',[name]) AS [Value] FROM sys.tables WHERE [name]<>'__EFMigrationsHistory' ORDER BY [Value]")
        .ToArrayAsync(TestContext.Current.CancellationToken);

    private async Task<string[]> ReadTriggersAsync() => await _context.Database.SqlQueryRaw<string>(
        "SELECT CONCAT(OBJECT_SCHEMA_NAME(t.[object_id]),'.',t.[name]) AS [Value] FROM sys.triggers t WHERE t.[parent_class]=1 ORDER BY [Value]")
        .ToArrayAsync(TestContext.Current.CancellationToken);

    private Task<int> ScalarAsync(string command) => _context.Database.SqlQueryRaw<int>(command)
        .SingleAsync(TestContext.Current.CancellationToken);

    private static readonly string[] ExpectedP0Migrations =
        ["20260711161500_InitialP0ProductionModel", "20260711161518_AddP0DatabaseProtections"];
    private static readonly string[] ExpectedP1Migrations =
        [.. ExpectedP0Migrations, "20260712001412_AddP1TeachingModel", "20260712010000_AddP1DatabaseProtections"];
    private static readonly string[] ExpectedP0Tables = ["academic.ClassGroup", "academic.Enrollment", "catalog.AcademicConfiguration", "catalog.AcademicYear",
        "catalog.DocumentType", "catalog.Grade", "catalog.School", "people.Person", "people.Student", "people.Teacher", "staff.TeacherContract"];
    private static readonly string[] ExpectedP1Tables =
        ["academic.ClassGroup", "academic.ClassSchedule", "academic.Enrollment", "academic.TeachingAssignment", "catalog.AcademicConfiguration",
            "catalog.AcademicYear", "catalog.DocumentType", "catalog.Grade", "catalog.School", "catalog.Subject", "people.Person", "people.Student",
            "people.Teacher", "staff.TeacherContract"];
    private static readonly string[] ExpectedP0Triggers = ["catalog.TR_AcademicConfiguration_PreventDelete", "catalog.TR_AcademicYear_ProtectCode",
        "catalog.TR_Grade_ProtectCode", "catalog.TR_School_ProtectStableValues"];
    private static readonly string[] ExpectedP1Triggers = [.. ExpectedP0Triggers, "catalog.TR_Subject_ProtectCode"];
}
