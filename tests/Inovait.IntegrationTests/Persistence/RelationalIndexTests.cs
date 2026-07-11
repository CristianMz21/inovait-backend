using Inovait.Infrastructure.Persistence;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Inovait.IntegrationTests.Persistence;

/// <summary>
/// Asserts, against real SQL Server metadata (<c>sys.indexes</c>/<c>sys.index_columns</c>) of a
/// migrated P0 database, that every declared index across the 11 P0 tables has exactly the key
/// column order, INCLUDE columns, uniqueness and (absence of) filter recorded in
/// <c>data-model.md</c>/the EF model snapshot; that no nonclustered index redundantly INCLUDEs
/// <c>Id</c> (already available through the clustered primary key); and that every foreign key is
/// covered by an index whose leading key columns match it.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P0")]
public sealed class RelationalIndexTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private InovaitDbContext _context = null!;

    [Fact]
    [Trait("Evidence", "IT-INDEXES-P0")]
    public async Task DeclaredIndexes_MatchExactNamesKeyOrderIncludesFiltersAndUniquenessAcrossAllElevenTables()
    {
        var actual = await ReadAsync(IndexesSql, TestContext.Current.CancellationToken);
        var expected = ExpectedIndexes.Select(index => index.ToRowText()).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(32, expected.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Evidence", "IT-INDEXES-P0")]
    public async Task NonclusteredIndexes_NeverCarryIdInIncludeBecauseTheClusteredPrimaryKeyAlreadyProvidesIt()
    {
        var offenders = await ReadAsync(NonclusteredIncludesIdSql, TestContext.Current.CancellationToken);
        Assert.Empty(offenders);
    }

    [Fact]
    [Trait("Evidence", "IT-INDEXES-P0")]
    public async Task EveryForeignKey_IsCoveredByAnIndexWhoseLeadingKeyColumnsMatchTheForeignKeyColumns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var foreignKeys = await ReadAsync(ForeignKeyColumnsSql, cancellationToken);
        Assert.Equal(11, foreignKeys.Length);

        var indexKeysByTable = (await ReadAsync(IndexKeyColumnsSql, cancellationToken))
            .Select(row => row.Split('|'))
            .GroupBy(parts => parts[0], parts => parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var row in foreignKeys)
        {
            var parts = row.Split('|');
            var table = parts[0];
            var foreignKeyName = parts[1];
            var foreignKeyColumns = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries);
            var candidates = indexKeysByTable.TryGetValue(table, out var keys) ? keys : [];
            var isCovered = candidates.Any(key => key.Length >= foreignKeyColumns.Length
                && key.Take(foreignKeyColumns.Length).SequenceEqual(foreignKeyColumns, StringComparer.Ordinal));
            Assert.True(isCovered,
                $"Foreign key {foreignKeyName} on {table} ({string.Join(",", foreignKeyColumns)}) has no leading-column supporting index.");
        }
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitIndexesP0_{Guid.NewGuid():N}",
        }.ConnectionString;
        _context = new InovaitDbContext(new DbContextOptionsBuilder<InovaitDbContext>()
            .UseSqlServer(connectionString).Options);
        await _context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    private async Task<string[]> ReadAsync(string sql, CancellationToken cancellationToken) =>
        await _context.Database.SqlQueryRaw<string>(sql).ToArrayAsync(cancellationToken);

    private sealed record IndexDefinition(string Schema, string Table, string Name, bool Unique, string[] Keys, string[] Includes)
    {
        public string ToRowText() =>
            $"{Schema}.{Table}.{Name}:{(Unique ? 1 : 0)}:{string.Join(',', Keys)}:{string.Join(',', Includes)}:";
    }

    // Data-driven expectation, sourced from the migration/model-snapshot declared shape of the
    // 11 P0 tables (data-model.md, InitialP0ProductionModel, InovaitDbContextModelSnapshot).
    // Every P0 index has no filter (HasFilter(null) on the one index that sets it explicitly).
    private static readonly IndexDefinition[] ExpectedIndexes =
    [
        new("catalog", "DocumentType", "PK_DocumentType", true, ["Id"], []),
        new("catalog", "DocumentType", "UQ_DocumentType_Code", true, ["Code"], []),

        new("catalog", "School", "PK_School", true, ["Id"], []),
        new("catalog", "School", "UQ_School_Code", true, ["Code"], []),
        new("catalog", "School", "UQ_School_Name", true, ["Name"], []),

        new("catalog", "AcademicYear", "PK_AcademicYear", true, ["Id"], []),
        new("catalog", "AcademicYear", "UQ_AcademicYear_Code", true, ["Code"], []),
        new("catalog", "AcademicYear", "UQ_AcademicYear_Name", true, ["Name"], []),

        new("catalog", "AcademicConfiguration", "PK_AcademicConfiguration", true, ["Id"], []),
        new("catalog", "AcademicConfiguration", "IX_AcademicConfiguration_CurrentAcademicYearId", false, ["CurrentAcademicYearId"], []),

        new("catalog", "Grade", "PK_Grade", true, ["Id"], []),
        new("catalog", "Grade", "UQ_Grade_Code", true, ["Code"], []),
        new("catalog", "Grade", "UQ_Grade_Name", true, ["Name"], []),
        new("catalog", "Grade", "UQ_Grade_SortOrder", true, ["SortOrder"], []),

        new("people", "Person", "PK_Person", true, ["Id"], []),
        new("people", "Person", "UQ_Person_DocumentTypeId_DocumentNumber", true, ["DocumentTypeId", "DocumentNumber"], []),
        new("people", "Person", "IX_Person_LastNames_FirstNames_Id", false, ["LastNames", "FirstNames", "Id"], ["DocumentTypeId", "DocumentNumber", "BirthDate"]),

        new("people", "Student", "PK_Student", true, ["PersonId"], []),

        new("people", "Teacher", "PK_Teacher", true, ["PersonId"], []),

        new("academic", "ClassGroup", "PK_ClassGroup", true, ["Id"], []),
        new("academic", "ClassGroup", "UQ_ClassGroup_Id_AcademicYear_ForEnrollment", true, ["Id", "AcademicYearId"], []),
        new("academic", "ClassGroup", "IX_ClassGroup_GradeId", false, ["GradeId"], []),
        new("academic", "ClassGroup", "IX_ClassGroup_AcademicYearId_GradeId_SchoolId", false, ["AcademicYearId", "GradeId", "SchoolId"], ["Code"]),
        new("academic", "ClassGroup", "UQ_ClassGroup_Context", true, ["SchoolId", "AcademicYearId", "GradeId", "Code"], []),

        new("academic", "Enrollment", "PK_Enrollment", true, ["Id"], []),
        new("academic", "Enrollment", "IX_Enrollment_ClassGroupId_AcademicYearId", false, ["ClassGroupId", "AcademicYearId"], []),
        new("academic", "Enrollment", "IX_Enrollment_ClassGroupId_StudentPersonId", false, ["ClassGroupId", "StudentPersonId"], ["AcademicYearId", "CreatedAtUtc"]),
        new("academic", "Enrollment", "UQ_Enrollment_StudentPersonId_AcademicYearId", true, ["StudentPersonId", "AcademicYearId"], []),

        new("staff", "TeacherContract", "PK_TeacherContract", true, ["Id"], []),
        new("staff", "TeacherContract", "IX_TeacherContract_SchoolId_StartDate_EndDate", false, ["SchoolId", "StartDate", "EndDate"], ["TeacherPersonId", "Status", "CancellationEffectiveDate"]),
        new("staff", "TeacherContract", "IX_TeacherContract_TeacherPersonId_StartDate_EndDate", false, ["TeacherPersonId", "StartDate", "EndDate"], ["SchoolId", "Status", "CancelledAtUtc", "CancellationReason", "CancellationEffectiveDate"]),
        new("staff", "TeacherContract", "UQ_TeacherContract_Exact", true, ["TeacherPersonId", "SchoolId", "StartDate", "EndDate"], []),
    ];

    // Metadata name columns (sys.tables/sys.columns/sys.indexes/...) carry the fixed SQL Server
    // catalog collation, which is independent of the database's own collation; mixing several of
    // them together (or with string literals) can otherwise raise "Cannot resolve collation
    // conflict", so every composite [Value] below is pinned to COLLATE DATABASE_DEFAULT.
    private const string IndexesSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',i.[name],':',i.[is_unique],':',
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

    private const string NonclusteredIncludesIdSql = """
        SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name],'.',i.[name]) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.indexes i
        JOIN sys.tables t ON t.[object_id]=i.[object_id]
        JOIN sys.index_columns ic ON ic.[object_id]=i.[object_id] AND ic.[index_id]=i.[index_id]
        JOIN sys.columns c ON c.[object_id]=ic.[object_id] AND c.[column_id]=ic.[column_id]
        WHERE i.[type]=2 AND ic.[is_included_column]=1 AND c.[name]=N'Id'
        """;

    private const string IndexKeyColumnsSql = """
        SELECT (CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name]) + '|' +
            ISNULL((SELECT STRING_AGG(c.[name], ',') WITHIN GROUP (ORDER BY ic.[key_ordinal])
                    FROM sys.index_columns ic JOIN sys.columns c ON c.[object_id]=ic.[object_id] AND c.[column_id]=ic.[column_id]
                    WHERE ic.[object_id]=i.[object_id] AND ic.[index_id]=i.[index_id] AND ic.[is_included_column]=0),'')) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.indexes i
        JOIN sys.tables t ON t.[object_id]=i.[object_id]
        WHERE i.[name] IS NOT NULL AND t.[name]<>'__EFMigrationsHistory'
        ORDER BY [Value]
        """;

    private const string ForeignKeyColumnsSql = """
        SELECT (CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name]) + '|' + fk.[name] + '|' +
            (SELECT STRING_AGG(pc.[name], ',') WITHIN GROUP (ORDER BY fkc.[constraint_column_id])
             FROM sys.foreign_key_columns fkc
             JOIN sys.columns pc ON pc.[object_id]=fkc.[parent_object_id] AND pc.[column_id]=fkc.[parent_column_id]
             WHERE fkc.[constraint_object_id]=fk.[object_id])) COLLATE DATABASE_DEFAULT AS [Value]
        FROM sys.foreign_keys fk
        JOIN sys.tables t ON t.[object_id]=fk.[parent_object_id]
        ORDER BY [Value]
        """;
}
