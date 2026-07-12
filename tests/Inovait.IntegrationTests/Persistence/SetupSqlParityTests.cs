using System.Diagnostics;
using Inovait.Infrastructure.Persistence;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Inovait.IntegrationTests.Persistence;

/// <summary>
/// Compares two independently produced databases — one from the full EF migration chain
/// (<c>InitialP0ProductionModel</c> + <c>AddP0DatabaseProtections</c> + <c>AddP1TeachingModel</c> +
/// <c>AddP1DatabaseProtections</c>), one from <c>database/setup.sql</c> run from scratch — and
/// asserts their relational metadata and canonical seed rows are identical across the full
/// 14-table universe. <c>database/setup.sql</c> is executed twice against its database to prove
/// it is safely idempotent before any comparison runs. The P0-only seed rows (5, unchanged by P1)
/// are asserted here; the P1-specific delta (Subject/TeachingAssignment/ClassSchedule shape,
/// TR_Subject_ProtectCode, and the empty-seed invariant on the three P1 tables) is asserted
/// separately by <see cref="SetupSqlParityTestsP1"/>.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P0")]
public sealed class SetupSqlParityTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private InovaitDbContext _migrated = null!;
    private InovaitDbContext _scripted = null!;

    [Fact]
    [Trait("Evidence", "IT-SQL-SCRIPT")]
    public async Task SetupScript_ProducesExactSysMetadataParityWithTheMigrationChain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var migratedSchemas = await ReadAsync(_migrated, SchemasSql, cancellationToken);
        Assert.Equal(["academic", "catalog", "people", "staff"], migratedSchemas);
        Assert.Equal(migratedSchemas, await ReadAsync(_scripted, SchemasSql, cancellationToken));

        var migratedTables = await ReadAsync(_migrated, TablesSql, cancellationToken);
        Assert.Equal(14, migratedTables.Length);
        Assert.Equal(migratedTables, await ReadAsync(_scripted, TablesSql, cancellationToken));

        Assert.Equal(await ReadAsync(_migrated, ColumnsSql, cancellationToken), await ReadAsync(_scripted, ColumnsSql, cancellationToken));
        Assert.Equal(await ReadAsync(_migrated, DefaultConstraintsSql, cancellationToken), await ReadAsync(_scripted, DefaultConstraintsSql, cancellationToken));
        Assert.Equal(await ReadAsync(_migrated, CheckConstraintsSql, cancellationToken), await ReadAsync(_scripted, CheckConstraintsSql, cancellationToken));
        Assert.Equal(await ReadAsync(_migrated, IndexesSql, cancellationToken), await ReadAsync(_scripted, IndexesSql, cancellationToken));
        Assert.Equal(await ReadAsync(_migrated, ForeignKeysSql, cancellationToken), await ReadAsync(_scripted, ForeignKeysSql, cancellationToken));

        var migratedTriggers = await ReadAsync(_migrated, TriggersSql, cancellationToken);
        Assert.Equal(5, migratedTriggers.Length);
        Assert.Equal(migratedTriggers, await ReadAsync(_scripted, TriggersSql, cancellationToken));

        // Extended properties (e.g. InovaitMigrationOwner, migration-only bookkeeping) are
        // never queried here, so they are excluded from this comparison by construction.
        var migratedPermissions = await ReadAsync(_migrated, RuntimePermissionsSql, cancellationToken);
        Assert.Equal(8, migratedPermissions.Length);
        Assert.Equal(migratedPermissions, await ReadAsync(_scripted, RuntimePermissionsSql, cancellationToken));
    }

    [Fact]
    [Trait("Evidence", "IT-SEED-P0")]
    public async Task SetupScript_SeedsTheSameFiveCanonicalRowsAsTheMigrationChain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var migratedSeed = await ReadAsync(_migrated, SeedRowsSql, cancellationToken);
        Assert.Equal(5, migratedSeed.Length);
        Assert.Equal(migratedSeed, await ReadAsync(_scripted, SeedRowsSql, cancellationToken));

        Assert.Equal(1, await _migrated.Database.SqlQueryRaw<int>(SingletonCoherenceSql).SingleAsync(cancellationToken));
        Assert.Equal(1, await _scripted.Database.SqlQueryRaw<int>(SingletonCoherenceSql).SingleAsync(cancellationToken));
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var script = await LoadSetupScriptAsync(cancellationToken);

        _migrated = CreateContext($"InovaitSetupParityMigrated_{Guid.NewGuid():N}");
        await _migrated.Database.MigrateAsync(cancellationToken);

        var scriptDatabaseName = $"InovaitSetupParityScript_{Guid.NewGuid():N}";
        await ExecuteOnMasterAsync($"CREATE DATABASE [{scriptDatabaseName}]", cancellationToken);
        _scripted = CreateContext(scriptDatabaseName);

        // Idempotency: a second run against an already-populated database, created by the
        // first run, must not throw and must not duplicate any schema/table/index/seed row —
        // every subsequent assertion in this class runs against this twice-applied database.
        await _scripted.Database.ExecuteSqlRawAsync(script, cancellationToken);
        await _scripted.Database.ExecuteSqlRawAsync(script, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _migrated.Database.EnsureDeletedAsync();
        await _scripted.Database.EnsureDeletedAsync();
        await _migrated.DisposeAsync();
        await _scripted.DisposeAsync();
    }

    private InovaitDbContext CreateContext(string databaseName)
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = databaseName,
        }.ConnectionString;
        return new InovaitDbContext(new DbContextOptionsBuilder<InovaitDbContext>()
            .UseSqlServer(connectionString).Options);
    }

    private async Task ExecuteOnMasterAsync(string sql, CancellationToken cancellationToken)
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = "master",
        }.ConnectionString;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string[]> ReadAsync(InovaitDbContext context, string sql, CancellationToken cancellationToken) =>
        await context.Database.SqlQueryRaw<string>(sql).ToArrayAsync(cancellationToken);

    private static async Task<string> LoadSetupScriptAsync(CancellationToken cancellationToken)
    {
        var repositoryRoot = (await RunGitAsync(AppContext.BaseDirectory, "rev-parse --show-toplevel", cancellationToken)).Trim();
        var scriptPath = Path.Combine(repositoryRoot, "database", "setup.sql");
        return await File.ReadAllTextAsync(scriptPath, cancellationToken);
    }

    private static async Task<string> RunGitAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output;
    }

    private const string SchemasSql =
        "SELECT [name] AS [Value] FROM sys.schemas WHERE [name] IN ('catalog','people','academic','staff') ORDER BY [name]";

    private const string TablesSql =
        "SELECT CONCAT(SCHEMA_NAME([schema_id]),'.',[name]) COLLATE DATABASE_DEFAULT AS [Value] FROM sys.tables WHERE [name]<>'__EFMigrationsHistory' ORDER BY [Value]";

    // Metadata name columns (sys.tables/sys.columns/sys.types/...) carry the fixed SQL Server
    // catalog collation, which is independent of the database's own collation; mixing several of
    // them together (or with string literals) can otherwise raise "Cannot resolve collation
    // conflict", so every composite [Value] below is pinned to COLLATE DATABASE_DEFAULT.
    private const string ColumnsSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',c.[name],':',ty.[name],':',c.[max_length],':',c.[precision],':',c.[scale],':',ISNULL(c.[collation_name],''),':',c.[is_nullable]) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.columns c
        JOIN sys.tables t ON t.[object_id]=c.[object_id]
        JOIN sys.types ty ON ty.[user_type_id]=c.[user_type_id]
        WHERE t.[name]<>'__EFMigrationsHistory'
        ORDER BY [Value]
        """;

    private const string DefaultConstraintsSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',c.[name],':',REPLACE(REPLACE(dc.[definition],' ',''),CHAR(10),'')) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.[object_id]=dc.[parent_object_id] AND c.[column_id]=dc.[parent_column_id]
        JOIN sys.tables t ON t.[object_id]=dc.[parent_object_id]
        ORDER BY [Value]
        """;

    private const string CheckConstraintsSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',cc.[name],':',REPLACE(REPLACE(REPLACE(cc.[definition],' ',''),CHAR(13),''),CHAR(10),'')) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.check_constraints cc
        JOIN sys.tables t ON t.[object_id]=cc.[parent_object_id]
        ORDER BY [Value]
        """;

    private const string IndexesSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',i.[name],':',i.[is_unique],':',i.[is_primary_key],':',
            ISNULL((SELECT STRING_AGG(CONCAT(c.[name],CASE WHEN ic.[is_descending_key]=1 THEN '-DESC' ELSE '' END), ',') WITHIN GROUP (ORDER BY ic.[key_ordinal])
                    FROM sys.index_columns ic JOIN sys.columns c ON c.[object_id]=ic.[object_id] AND c.[column_id]=ic.[column_id]
                    WHERE ic.[object_id]=i.[object_id] AND ic.[index_id]=i.[index_id] AND ic.[is_included_column]=0),''),':',
            ISNULL((SELECT STRING_AGG(c.[name], ',') WITHIN GROUP (ORDER BY ic.[index_column_id])
                    FROM sys.index_columns ic JOIN sys.columns c ON c.[object_id]=ic.[object_id] AND c.[column_id]=ic.[column_id]
                    WHERE ic.[object_id]=i.[object_id] AND ic.[index_id]=i.[index_id] AND ic.[is_included_column]=1),''),':',
            ISNULL(REPLACE(REPLACE(i.[filter_definition],' ',''),CHAR(10),''),'')) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.indexes i
        JOIN sys.tables t ON t.[object_id]=i.[object_id]
        WHERE i.[name] IS NOT NULL AND t.[name]<>'__EFMigrationsHistory'
        ORDER BY [Value]
        """;

    private const string ForeignKeysSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',fk.[name],':',fk.[delete_referential_action_desc],':',
            (SELECT STRING_AGG(CONCAT(pc.[name],'->',rc.[name]), ',') WITHIN GROUP (ORDER BY fkc.[constraint_column_id])
             FROM sys.foreign_key_columns fkc
             JOIN sys.columns pc ON pc.[object_id]=fkc.[parent_object_id] AND pc.[column_id]=fkc.[parent_column_id]
             JOIN sys.columns rc ON rc.[object_id]=fkc.[referenced_object_id] AND rc.[column_id]=fkc.[referenced_column_id]
             WHERE fkc.[constraint_object_id]=fk.[object_id])) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.foreign_keys fk
        JOIN sys.tables t ON t.[object_id]=fk.[parent_object_id]
        ORDER BY [Value]
        """;

    private const string TriggersSql =
        "SELECT CONCAT(OBJECT_SCHEMA_NAME(tr.[object_id]),'.',tr.[name]) COLLATE DATABASE_DEFAULT AS [Value] FROM sys.triggers tr WHERE tr.[parent_class]=1 ORDER BY [Value]";

    private const string RuntimePermissionsSql = """
        SELECT CONCAT(OBJECT_SCHEMA_NAME(p.[major_id]),'.',OBJECT_NAME(p.[major_id]),':',p.[permission_name],':',p.[state_desc]) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.database_permissions p
        WHERE p.[grantee_principal_id]=DATABASE_PRINCIPAL_ID(N'inovait_runtime') AND p.[class]=1
            AND OBJECT_NAME(p.[major_id]) IN ('DocumentType','AcademicConfiguration')
        ORDER BY [Value]
        """;

    private const string SeedRowsSql = """
        SELECT [Value] FROM (
            SELECT 1 AS [Ord], CONCAT('School:',[Id],':',[Code],':',[Name],':',[Sector],':',CONVERT(varchar(23),[CreatedAtUtc],126),':',CONVERT(varchar(23),[UpdatedAtUtc],126)) AS [Value] FROM [catalog].[School]
            UNION ALL
            SELECT 2, CONCAT('AcademicYear:',[Id],':',[Code],':',[Name],':',CONVERT(varchar(10),[StartDate],23),':',CONVERT(varchar(10),[EndDate],23),':',CONVERT(varchar(23),[CreatedAtUtc],126),':',CONVERT(varchar(23),[UpdatedAtUtc],126)) FROM [catalog].[AcademicYear]
            UNION ALL
            SELECT 3, CONCAT('Grade:',[Id],':',[Code],':',[Name],':',[SortOrder],':',CONVERT(varchar(23),[CreatedAtUtc],126),':',CONVERT(varchar(23),[UpdatedAtUtc],126)) FROM [catalog].[Grade]
            UNION ALL
            SELECT 4, CONCAT('DocumentType:',[Id],':',[Code],':',[Name],':',[IsActive]) FROM [catalog].[DocumentType]
            UNION ALL
            SELECT 5, CONCAT('AcademicConfiguration:',[Id],':',[CurrentAcademicYearId]) FROM [catalog].[AcademicConfiguration]
        ) rows ORDER BY [Ord]
        """;

    private const string SingletonCoherenceSql =
        "SELECT COUNT(*) AS [Value] FROM [catalog].[AcademicConfiguration] ac JOIN [catalog].[AcademicYear] ay ON ay.[Id]=ac.[CurrentAcademicYearId] WHERE ac.[Id]=1";
}

/// <summary>
/// The P1 delta of <see cref="SetupSqlParityTests"/>: focuses the same migrated-vs-scripted
/// comparison specifically on the three P1 tables (columns/defaults/checks/indexes/FKs and the
/// <c>TR_Subject_ProtectCode</c> trigger) and on the P1 seed invariant — no fictitious rows are
/// produced for <c>Subject</c>/<c>TeachingAssignment</c>/<c>ClassSchedule</c> by either route,
/// per data-model.md's setup.sql responsibilities section (only the fifth trigger is P1's job)
/// — while reconfirming the five P0 canonical seed rows are undisturbed. Also proves
/// <c>TR_Subject_ProtectCode</c> (error 51007) blocks a <c>Subject.Code</c> update on the migrated
/// path; <see cref="CatalogModelTests"/> proves the same trigger on the EnsureCreated path.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P1")]
public sealed class SetupSqlParityTestsP1(SqlServerFixture fixture) : IAsyncLifetime
{
    private InovaitDbContext _migrated = null!;
    private InovaitDbContext _scripted = null!;

    [Fact]
    [Trait("Evidence", "IT-SQL-SCRIPT-P1")]
    public async Task SetupScript_ProducesExactP1TableAndTriggerParityWithTheMigrationChain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var migratedTables = await ReadAsync(_migrated, P1TablesSql, cancellationToken);
        Assert.Equal(["academic.ClassSchedule", "academic.TeachingAssignment", "catalog.Subject"], migratedTables);
        Assert.Equal(migratedTables, await ReadAsync(_scripted, P1TablesSql, cancellationToken));

        Assert.Equal(await ReadAsync(_migrated, P1ColumnsSql, cancellationToken), await ReadAsync(_scripted, P1ColumnsSql, cancellationToken));
        Assert.Equal(await ReadAsync(_migrated, P1DefaultConstraintsSql, cancellationToken), await ReadAsync(_scripted, P1DefaultConstraintsSql, cancellationToken));
        Assert.Equal(await ReadAsync(_migrated, P1CheckConstraintsSql, cancellationToken), await ReadAsync(_scripted, P1CheckConstraintsSql, cancellationToken));
        Assert.Equal(await ReadAsync(_migrated, P1IndexesSql, cancellationToken), await ReadAsync(_scripted, P1IndexesSql, cancellationToken));
        Assert.Equal(await ReadAsync(_migrated, P1ForeignKeysSql, cancellationToken), await ReadAsync(_scripted, P1ForeignKeysSql, cancellationToken));

        Assert.Equal(["catalog.TR_Subject_ProtectCode"], await ReadAsync(_migrated, P1TriggerSql, cancellationToken));
        Assert.Equal(["catalog.TR_Subject_ProtectCode"], await ReadAsync(_scripted, P1TriggerSql, cancellationToken));

        await _migrated.Database.ExecuteSqlRawAsync(
            "INSERT [catalog].[Subject] ([Code],[Name]) VALUES ('SUB-PARITY',N'Parity Subject')", cancellationToken);
        var exception = await Assert.ThrowsAsync<SqlException>(() => _migrated.Database.ExecuteSqlRawAsync(
            "UPDATE [catalog].[Subject] SET [Code]='sub-parity' WHERE [Code]='SUB-PARITY'", cancellationToken));
        Assert.Equal(51007, exception.Number);
    }

    [Fact]
    [Trait("Evidence", "IT-SEED-P1")]
    public async Task SetupScript_SeedsNoFictitiousP1RowsAndPreservesTheFiveP0CanonicalRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(0, await _migrated.Database.SqlQueryRaw<int>(P1RowCountSql).SingleAsync(cancellationToken));
        Assert.Equal(0, await _scripted.Database.SqlQueryRaw<int>(P1RowCountSql).SingleAsync(cancellationToken));

        var migratedP0Seed = await ReadAsync(_migrated, P0SeedRowsSql, cancellationToken);
        Assert.Equal(5, migratedP0Seed.Length);
        Assert.Equal(migratedP0Seed, await ReadAsync(_scripted, P0SeedRowsSql, cancellationToken));
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var script = await LoadSetupScriptAsync(cancellationToken);

        _migrated = CreateContext($"InovaitSetupParityP1Migrated_{Guid.NewGuid():N}");
        await _migrated.Database.MigrateAsync(cancellationToken);

        var scriptDatabaseName = $"InovaitSetupParityP1Script_{Guid.NewGuid():N}";
        await ExecuteOnMasterAsync($"CREATE DATABASE [{scriptDatabaseName}]", cancellationToken);
        _scripted = CreateContext(scriptDatabaseName);

        await _scripted.Database.ExecuteSqlRawAsync(script, cancellationToken);
        await _scripted.Database.ExecuteSqlRawAsync(script, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _migrated.Database.EnsureDeletedAsync();
        await _scripted.Database.EnsureDeletedAsync();
        await _migrated.DisposeAsync();
        await _scripted.DisposeAsync();
    }

    private InovaitDbContext CreateContext(string databaseName)
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = databaseName,
        }.ConnectionString;
        return new InovaitDbContext(new DbContextOptionsBuilder<InovaitDbContext>()
            .UseSqlServer(connectionString).Options);
    }

    private async Task ExecuteOnMasterAsync(string sql, CancellationToken cancellationToken)
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = "master",
        }.ConnectionString;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string[]> ReadAsync(InovaitDbContext context, string sql, CancellationToken cancellationToken) =>
        await context.Database.SqlQueryRaw<string>(sql).ToArrayAsync(cancellationToken);

    private static async Task<string> LoadSetupScriptAsync(CancellationToken cancellationToken)
    {
        var repositoryRoot = (await RunGitAsync(AppContext.BaseDirectory, "rev-parse --show-toplevel", cancellationToken)).Trim();
        var scriptPath = Path.Combine(repositoryRoot, "database", "setup.sql");
        return await File.ReadAllTextAsync(scriptPath, cancellationToken);
    }

    private static async Task<string> RunGitAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output;
    }

    private const string P1TablesSql =
        "SELECT CONCAT(SCHEMA_NAME([schema_id]),'.',[name]) COLLATE DATABASE_DEFAULT AS [Value] FROM sys.tables WHERE [name] IN ('Subject','TeachingAssignment','ClassSchedule') ORDER BY [Value]";

    private const string P1ColumnsSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',c.[name],':',ty.[name],':',c.[max_length],':',c.[precision],':',c.[scale],':',ISNULL(c.[collation_name],''),':',c.[is_nullable]) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.columns c
        JOIN sys.tables t ON t.[object_id]=c.[object_id]
        JOIN sys.types ty ON ty.[user_type_id]=c.[user_type_id]
        WHERE t.[name] IN ('Subject','TeachingAssignment','ClassSchedule')
        ORDER BY [Value]
        """;

    private const string P1DefaultConstraintsSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',c.[name],':',REPLACE(REPLACE(dc.[definition],' ',''),CHAR(10),'')) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.[object_id]=dc.[parent_object_id] AND c.[column_id]=dc.[parent_column_id]
        JOIN sys.tables t ON t.[object_id]=dc.[parent_object_id]
        WHERE t.[name] IN ('Subject','TeachingAssignment','ClassSchedule')
        ORDER BY [Value]
        """;

    private const string P1CheckConstraintsSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',cc.[name],':',REPLACE(REPLACE(REPLACE(cc.[definition],' ',''),CHAR(13),''),CHAR(10),'')) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.check_constraints cc
        JOIN sys.tables t ON t.[object_id]=cc.[parent_object_id]
        WHERE t.[name] IN ('Subject','TeachingAssignment','ClassSchedule')
        ORDER BY [Value]
        """;

    private const string P1IndexesSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',i.[name],':',i.[is_unique],':',i.[is_primary_key],':',
            ISNULL((SELECT STRING_AGG(CONCAT(c.[name],CASE WHEN ic.[is_descending_key]=1 THEN '-DESC' ELSE '' END), ',') WITHIN GROUP (ORDER BY ic.[key_ordinal])
                    FROM sys.index_columns ic JOIN sys.columns c ON c.[object_id]=ic.[object_id] AND c.[column_id]=ic.[column_id]
                    WHERE ic.[object_id]=i.[object_id] AND ic.[index_id]=i.[index_id] AND ic.[is_included_column]=0),''),':',
            ISNULL((SELECT STRING_AGG(c.[name], ',') WITHIN GROUP (ORDER BY ic.[index_column_id])
                    FROM sys.index_columns ic JOIN sys.columns c ON c.[object_id]=ic.[object_id] AND c.[column_id]=ic.[column_id]
                    WHERE ic.[object_id]=i.[object_id] AND ic.[index_id]=i.[index_id] AND ic.[is_included_column]=1),''),':',
            ISNULL(REPLACE(REPLACE(i.[filter_definition],' ',''),CHAR(10),''),'')) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.indexes i
        JOIN sys.tables t ON t.[object_id]=i.[object_id]
        WHERE i.[name] IS NOT NULL AND t.[name] IN ('Subject','TeachingAssignment','ClassSchedule')
        ORDER BY [Value]
        """;

    private const string P1ForeignKeysSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',fk.[name],':',fk.[delete_referential_action_desc],':',
            (SELECT STRING_AGG(CONCAT(pc.[name],'->',rc.[name]), ',') WITHIN GROUP (ORDER BY fkc.[constraint_column_id])
             FROM sys.foreign_key_columns fkc
             JOIN sys.columns pc ON pc.[object_id]=fkc.[parent_object_id] AND pc.[column_id]=fkc.[parent_column_id]
             JOIN sys.columns rc ON rc.[object_id]=fkc.[referenced_object_id] AND rc.[column_id]=fkc.[referenced_column_id]
             WHERE fkc.[constraint_object_id]=fk.[object_id])) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.foreign_keys fk
        JOIN sys.tables t ON t.[object_id]=fk.[parent_object_id]
        WHERE t.[name] IN ('Subject','TeachingAssignment','ClassSchedule')
        ORDER BY [Value]
        """;

    private const string P1TriggerSql =
        "SELECT CONCAT(OBJECT_SCHEMA_NAME(tr.[object_id]),'.',tr.[name]) COLLATE DATABASE_DEFAULT AS [Value] FROM sys.triggers tr WHERE tr.[parent_class]=1 AND tr.[name]=N'TR_Subject_ProtectCode' ORDER BY [Value]";

    private const string P1RowCountSql =
        "SELECT (SELECT COUNT(*) FROM [catalog].[Subject])+(SELECT COUNT(*) FROM [academic].[TeachingAssignment])+(SELECT COUNT(*) FROM [academic].[ClassSchedule]) AS [Value]";

    private const string P0SeedRowsSql = """
        SELECT [Value] FROM (
            SELECT 1 AS [Ord], CONCAT('School:',[Id],':',[Code],':',[Name],':',[Sector],':',CONVERT(varchar(23),[CreatedAtUtc],126),':',CONVERT(varchar(23),[UpdatedAtUtc],126)) AS [Value] FROM [catalog].[School]
            UNION ALL
            SELECT 2, CONCAT('AcademicYear:',[Id],':',[Code],':',[Name],':',CONVERT(varchar(10),[StartDate],23),':',CONVERT(varchar(10),[EndDate],23),':',CONVERT(varchar(23),[CreatedAtUtc],126),':',CONVERT(varchar(23),[UpdatedAtUtc],126)) FROM [catalog].[AcademicYear]
            UNION ALL
            SELECT 3, CONCAT('Grade:',[Id],':',[Code],':',[Name],':',[SortOrder],':',CONVERT(varchar(23),[CreatedAtUtc],126),':',CONVERT(varchar(23),[UpdatedAtUtc],126)) FROM [catalog].[Grade]
            UNION ALL
            SELECT 4, CONCAT('DocumentType:',[Id],':',[Code],':',[Name],':',[IsActive]) FROM [catalog].[DocumentType]
            UNION ALL
            SELECT 5, CONCAT('AcademicConfiguration:',[Id],':',[CurrentAcademicYearId]) FROM [catalog].[AcademicConfiguration]
        ) rows ORDER BY [Ord]
        """;
}
