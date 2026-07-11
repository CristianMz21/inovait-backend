using Inovait.Core.Domain.Catalogs;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Persistence;
using Inovait.Infrastructure.Persistence.Seed;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Inovait.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P0")]
public sealed class CatalogModelTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string CanonicalSeedSql = "SELECT (SELECT COUNT(*) FROM [catalog].[School] WHERE [Id]=1 AND [Code]='SCH-001' AND [Name]=N'North Learning Center' AND [Sector]='Public' AND [CreatedAtUtc]='2026-01-01' AND [UpdatedAtUtc]='2026-01-01')+(SELECT COUNT(*) FROM [catalog].[AcademicYear] WHERE [Id]=1 AND [Code]='AY-2026' AND [Name]=N'Academic Year 2026' AND [StartDate]='2026-01-01' AND [EndDate]='2026-12-31' AND [CreatedAtUtc]='2026-01-01' AND [UpdatedAtUtc]='2026-01-01')+(SELECT COUNT(*) FROM [catalog].[Grade] WHERE [Id]=1 AND [Code]='G01' AND [Name]=N'First Grade' AND [SortOrder]=1 AND [CreatedAtUtc]='2026-01-01' AND [UpdatedAtUtc]='2026-01-01')+(SELECT COUNT(*) FROM [catalog].[DocumentType] WHERE [Id]=1 AND [Code]='CC' AND [Name]=N'Citizenship Card' AND [IsActive]=1)+(SELECT COUNT(*) FROM [catalog].[AcademicConfiguration] WHERE [Id]=1 AND [CurrentAcademicYearId]=1) AS [Value]";
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;

    [Fact]
    [Trait("Evidence", "IT-CATALOG-SCHEMA-S03")]
    public async Task CatalogSchema_UsesCanonicalPhysicalMappings()
    {
        var tables = await _context.Database.SqlQueryRaw<string>(
            "SELECT CONCAT(SCHEMA_NAME([schema_id]), '.', [name]) AS [Value] FROM sys.tables WHERE [schema_id]=SCHEMA_ID('catalog') ORDER BY [name]")
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["catalog.AcademicConfiguration", "catalog.AcademicYear", "catalog.DocumentType", "catalog.Grade", "catalog.School"], tables);

        Type[] entityTypes = [typeof(School), typeof(AcademicYear), typeof(AcademicConfiguration), typeof(Grade), typeof(DocumentType)];
        foreach (var type in entityTypes)
        {
            var entity = Model.FindEntityType(type)!;
            Assert.Equal("catalog", entity.GetSchema());
            var key = Assert.Single(entity.FindPrimaryKey()!.Properties);
            Assert.Equal("Id", key.Name);
            Assert.Equal($"PK_{entity.GetTableName()}", entity.FindPrimaryKey()!.GetName());
            Assert.Equal(type == typeof(AcademicConfiguration) ? "tinyint" : type == typeof(DocumentType) ? "smallint" : "int", key.GetColumnType());
        }

        const string collation = "Latin1_General_100_CI_AS";
        (Type Entity, string Name, string StoreType, int? MaxLength, string? Collation)[] properties =
        [
            (typeof(School), nameof(School.Code), "varchar(20)", 20, collation),
            (typeof(School), nameof(School.Name), "nvarchar(160)", 160, collation), (typeof(School), nameof(School.Sector), "varchar(8)", 8, collation),
            (typeof(AcademicYear), nameof(AcademicYear.Code), "varchar(20)", 20, collation), (typeof(AcademicYear), nameof(AcademicYear.Name), "nvarchar(80)", 80, collation),
            (typeof(AcademicYear), nameof(AcademicYear.StartDate), "date", null, null), (typeof(AcademicYear), nameof(AcademicYear.EndDate), "date", null, null),
            (typeof(AcademicConfiguration), nameof(AcademicConfiguration.CurrentAcademicYearId), "int", null, null),
            (typeof(Grade), nameof(Grade.Code), "varchar(20)", 20, collation), (typeof(Grade), nameof(Grade.Name), "nvarchar(80)", 80, collation),
            (typeof(Grade), nameof(Grade.SortOrder), "smallint", null, null),
            (typeof(DocumentType), nameof(DocumentType.Code), "varchar(20)", 20, collation), (typeof(DocumentType), nameof(DocumentType.Name), "nvarchar(80)", 80, collation),
            (typeof(DocumentType), nameof(DocumentType.IsActive), "bit", null, null),
        ];
        foreach (var mapping in properties)
        {
            var property = Model.FindEntityType(mapping.Entity)!.FindProperty(mapping.Name)!;
            Assert.Equal(mapping.StoreType, property.GetColumnType());
            Assert.Equal(mapping.MaxLength, property.GetMaxLength());
            Assert.Equal(mapping.Collation, property.GetCollation());
        }

        Assert.Equal(["Code", "Name"], UniqueIndexes<School>());
        Assert.Equal(["Code", "Name"], UniqueIndexes<AcademicYear>());
        Assert.Equal(["Code", "Name", "SortOrder"], UniqueIndexes<Grade>());
        Assert.Equal(["Code"], UniqueIndexes<DocumentType>());

        foreach (var type in new[] { typeof(School), typeof(AcademicYear), typeof(Grade) })
        {
            var entity = Model.FindEntityType(type)!;
            foreach (var name in new[] { nameof(School.CreatedAtUtc), nameof(School.UpdatedAtUtc) })
            {
                var timestamp = entity.FindProperty(name)!;
                Assert.Equal("datetime2(3)", timestamp.GetColumnType());
                Assert.Equal("SYSUTCDATETIME()", timestamp.GetDefaultValueSql());
            }

            var rowVersion = entity.FindProperty(nameof(School.RowVersion))!;
            Assert.Equal("rowversion", rowVersion.GetColumnType());
            Assert.True(rowVersion.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        }

        foreach (var type in new[] { typeof(AcademicConfiguration), typeof(DocumentType) })
        {
            var entity = Model.FindEntityType(type)!;
            Assert.Null(entity.FindProperty(nameof(School.CreatedAtUtc)));
            Assert.Null(entity.FindProperty(nameof(School.RowVersion)));
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        const string retainedRowsSql = "SELECT CONCAT(CONVERT(varchar(18),s.[RowVersion],1),':',CONVERT(varchar(18),g.[RowVersion],1)) AS [Value] FROM [catalog].[School] s CROSS JOIN [catalog].[Grade] g";
        var retainedVersions = await _context.Database.SqlQueryRaw<string>(retainedRowsSql).SingleAsync(cancellationToken);
        await _context.Database.ExecuteSqlRawAsync("DISABLE TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete] ON [catalog].[AcademicConfiguration]; DELETE FROM [catalog].[AcademicConfiguration]; ENABLE TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete] ON [catalog].[AcademicConfiguration]; DELETE FROM [catalog].[DocumentType]; DELETE FROM [catalog].[AcademicYear]", cancellationToken);
        _context.ChangeTracker.Clear();
        await ProductionCatalogSeed.ApplyAsync(_context, cancellationToken);
        Assert.Equal(retainedVersions, await _context.Database.SqlQueryRaw<string>(retainedRowsSql).SingleAsync(cancellationToken));
        Assert.Equal(5, await CanonicalSeedCountAsync(_context, cancellationToken));

        await _context.Database.ExecuteSqlRawAsync("DISABLE TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete] ON [catalog].[AcademicConfiguration]; DELETE FROM [catalog].[AcademicConfiguration]; ENABLE TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete] ON [catalog].[AcademicConfiguration]; DELETE FROM [catalog].[AcademicYear]; SET IDENTITY_INSERT [catalog].[AcademicYear] ON; INSERT [catalog].[AcademicYear] ([Id],[Code],[Name],[StartDate],[EndDate],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (2,'AY-2026',N'conflict','2026-01-01','2026-12-31','2026-01-01','2026-01-01'); SET IDENTITY_INSERT [catalog].[AcademicYear] OFF", cancellationToken);
        var seedConflict = await Assert.ThrowsAsync<SqlException>(() => ProductionCatalogSeed.ApplyAsync(_context, cancellationToken));
        Assert.Equal(51010, seedConflict.Number);

        var invalid = await Assert.ThrowsAsync<SqlException>(() => _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO [catalog].[Grade] ([Code],[Name],[SortOrder]) VALUES ('BAD','   ',99)", cancellationToken));
        Assert.Equal(547, invalid.Number);

        await _context.Database.ExecuteSqlRawAsync(
            "DROP ROLE [inovait_runtime]; CREATE USER [inovait_runtime] WITHOUT LOGIN", cancellationToken);
        var unsafePrincipal = await Assert.ThrowsAsync<SqlException>(() =>
            CatalogDatabaseProtections.InstallAsync(_context.Database, cancellationToken));
        Assert.Equal(51005, unsafePrincipal.Number);
    }

    [Fact]
    public async Task ProductionSeed_ConcurrentCallersCompleteDeterministically()
    {
        await ClearCatalogsAsync();
        await using var first = CreateContext();
        await using var second = CreateContext();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await Task.WhenAll(ProductionCatalogSeed.ApplyAsync(first, timeout.Token), ProductionCatalogSeed.ApplyAsync(second, timeout.Token));
        Assert.Equal(5, await CanonicalSeedCountAsync(_context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProductionSeed_FailureRollsBackCleansIdentityInsertAndRetries()
    {
        await ClearCatalogsAsync();
        await _context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await _context.Database.ExecuteSqlRawAsync("CREATE OR ALTER TRIGGER [catalog].[TR_AcademicYear_SeedFailure] ON [catalog].[AcademicYear] AFTER INSERT AS THROW 51020,'Injected seed failure.',1", TestContext.Current.CancellationToken);
        var failure = await Assert.ThrowsAsync<SqlException>(() => ProductionCatalogSeed.ApplyAsync(_context, TestContext.Current.CancellationToken));
        Assert.Equal(51020, failure.Number);
        Assert.Equal(0, await CanonicalSeedCountAsync(_context, TestContext.Current.CancellationToken));
        await _context.Database.ExecuteSqlRawAsync("DROP TRIGGER [catalog].[TR_AcademicYear_SeedFailure]", TestContext.Current.CancellationToken);
        await ProductionCatalogSeed.ApplyAsync(_context, TestContext.Current.CancellationToken);
        Assert.Equal(5, await CanonicalSeedCountAsync(_context, TestContext.Current.CancellationToken));
        await _context.Database.CloseConnectionAsync();
    }

    [Fact]
    [Trait("Evidence", "IT-CATALOG-MUTABILITY-S03")]
    public async Task CatalogMutability_EfRejectsStableValueChangesButPersistsNameChanges()
    {
        Assert.Equal(PropertySaveBehavior.Throw, AfterSave<School>(nameof(School.Code)));
        Assert.Equal(PropertySaveBehavior.Throw, AfterSave<School>(nameof(School.Sector)));
        Assert.Equal(PropertySaveBehavior.Throw, AfterSave<AcademicYear>(nameof(AcademicYear.Code)));
        Assert.Equal(PropertySaveBehavior.Throw, AfterSave<Grade>(nameof(Grade.Code)));
        Assert.Equal(PropertySaveBehavior.Save, AfterSave<School>(nameof(School.Name)));

        var school = new School("SCH-01", "Original name", SchoolSector.Public);
        _context.Add(school);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.All([school.CreatedAtUtc != default, school.UpdatedAtUtc != default, school.RowVersion.Length > 0], Assert.True);
        school.Name = "Updated name";
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Updated name", school.Name);

        _context.Entry(school).Property(entity => entity.Code).CurrentValue = "SCH-02";
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _context.SaveChangesAsync(TestContext.Current.CancellationToken));
        _context.ChangeTracker.Clear();
        Assert.Equal("SCH-01", (await _context.Schools.SingleAsync(
            entity => entity.Id == school.Id, TestContext.Current.CancellationToken)).Code);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE [catalog].[School] SET [Name]=N'Allowed SQL name' WHERE [Code]='SCH-01'; UPDATE [catalog].[AcademicYear] SET [Name]=N'Allowed year name' WHERE [Id]=1; UPDATE [catalog].[Grade] SET [Name]=N'Allowed grade name' WHERE [Id]=1",
            TestContext.Current.CancellationToken);
        Assert.Equal("Allowed SQL name", await _context.Schools.Where(entity => entity.Code == "SCH-01")
            .Select(entity => entity.Name).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Allowed year name", await _context.AcademicYears.Select(entity => entity.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Allowed grade name", await _context.Grades.Select(entity => entity.Name)
            .SingleAsync(TestContext.Current.CancellationToken));

        await AssertProtectedUpdate("UPDATE [catalog].[School] SET [Code]='sch-001' WHERE [Code]='SCH-001'", 51001);
        await AssertProtectedUpdate("UPDATE [catalog].[School] SET [Sector]='public' WHERE [Code]='SCH-001'", 51001);
        await AssertProtectedUpdate("UPDATE [catalog].[AcademicYear] SET [Code]='ay-2026' WHERE [Code]='AY-2026'", 51002);
        await AssertProtectedUpdate("UPDATE [catalog].[Grade] SET [Code]='g01' WHERE [Code]='G01'", 51003);
    }

    [Fact]
    [Trait("Evidence", "IT-CATALOG-SINGLETON-S03")]
    public async Task AcademicConfiguration_UsesTinyIntSingletonCheckAndNoActionForeignKey()
    {
        var entity = Model.FindEntityType(typeof(AcademicConfiguration))!;
        Assert.Equal(DeleteBehavior.NoAction, Assert.Single(entity.GetForeignKeys()).DeleteBehavior);
        _context.Add(new AcademicConfiguration(2, 1));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(547, Assert.IsType<SqlException>(exception.InnerException).Number);
        _context.ChangeTracker.Clear();

        var check = _scope.ServiceProvider.GetRequiredService<AcademicConfigurationStartupCheck>();
        await check.EnsurePresentAsync(TestContext.Current.CancellationToken);
        _context.Add(new AcademicYear("AY-2027", "Academic Year 2027", new(2027, 1, 1), new(2027, 12, 31)));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await _context.Database.ExecuteSqlRawAsync(
            "CREATE USER [inovait_runtime_test] WITHOUT LOGIN; ALTER ROLE [inovait_runtime] ADD MEMBER [inovait_runtime_test]",
            TestContext.Current.CancellationToken);
        await _context.Database.ExecuteSqlRawAsync(
            "EXECUTE AS USER=N'inovait_runtime_test'; SELECT COUNT_BIG(*) FROM [catalog].[DocumentType]; REVERT",
            TestContext.Current.CancellationToken);
        await _context.Database.ExecuteSqlRawAsync(
            "BEGIN TRY EXECUTE AS USER=N'inovait_runtime_test'; UPDATE [catalog].[AcademicConfiguration] SET [CurrentAcademicYearId]=2 WHERE [Id]=1; IF (SELECT COUNT(*) FROM [catalog].[AcademicConfiguration] WHERE [Id]=1 AND [CurrentAcademicYearId]=2)<>1 THROW 51006,'Runtime singleton update failed.',1; REVERT; END TRY BEGIN CATCH IF USER_NAME()=N'inovait_runtime_test' REVERT; THROW; END CATCH",
            TestContext.Current.CancellationToken);
        var configuration = await _context.AcademicConfigurations.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((byte)1, configuration.Id);
        Assert.Equal(2, configuration.CurrentAcademicYearId);
        await using (var healthyFactory = CreateProductionFactory())
        {
            using var response = await healthyFactory.CreateClient().GetAsync("/health", TestContext.Current.CancellationToken);
            Assert.True(response.IsSuccessStatusCode);
        }
        await AssertRuntimeWriteDenied("INSERT INTO [catalog].[DocumentType] ([Code],[Name],[IsActive]) VALUES ('PP',N'Passport',1)");
        await AssertRuntimeWriteDenied("UPDATE [catalog].[DocumentType] SET [Name]=N'blocked' WHERE [Id]=1");
        await AssertRuntimeWriteDenied("DELETE FROM [catalog].[DocumentType] WHERE [Id]=1");
        await AssertRuntimeWriteDenied("INSERT INTO [catalog].[AcademicConfiguration] ([Id],[CurrentAcademicYearId]) VALUES (2,1)");
        await AssertRuntimeWriteDenied("DELETE FROM [catalog].[AcademicConfiguration] WHERE [Id]=1");
        var delete = await Assert.ThrowsAsync<SqlException>(() => _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM [catalog].[AcademicConfiguration] WHERE [Id]=1", TestContext.Current.CancellationToken));
        Assert.Equal(51004, delete.Number);

        await _context.Database.ExecuteSqlRawAsync(
            "DISABLE TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete] ON [catalog].[AcademicConfiguration]; DELETE FROM [catalog].[AcademicConfiguration]; ENABLE TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete] ON [catalog].[AcademicConfiguration]",
            TestContext.Current.CancellationToken);
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            check.EnsurePresentAsync(TestContext.Current.CancellationToken));
        Assert.Contains("AcademicConfiguration(Id=1)", missing.Message, StringComparison.Ordinal);

        await using var factory = CreateProductionFactory();
        var startupFailure = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("AcademicConfiguration(Id=1)", startupFailure.ToString(), StringComparison.Ordinal);
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitS03A_{Guid.NewGuid():N}",
        }.ConnectionString;
        _provider = new ServiceCollection().AddInovaitInfrastructure(connectionString)
            .BuildServiceProvider(validateScopes: true);
        _scope = _provider.CreateAsyncScope();
        _context = _scope.ServiceProvider.GetRequiredService<InovaitDbContext>();
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM [catalog].[AcademicConfiguration]; DELETE FROM [catalog].[DocumentType]; DELETE FROM [catalog].[Grade]; DELETE FROM [catalog].[School]; DELETE FROM [catalog].[AcademicYear]; DBCC CHECKIDENT ('catalog.DocumentType', RESEED, 0); DBCC CHECKIDENT ('catalog.Grade', RESEED, 0); DBCC CHECKIDENT ('catalog.School', RESEED, 0); DBCC CHECKIDENT ('catalog.AcademicYear', RESEED, 0)",
            TestContext.Current.CancellationToken);
        await ProductionCatalogSeed.ApplyAsync(_context, TestContext.Current.CancellationToken);
        await ProductionCatalogSeed.ApplyAsync(_context, TestContext.Current.CancellationToken);
        await CatalogDatabaseProtections.InstallAsync(_context.Database, TestContext.Current.CancellationToken);
        await CatalogDatabaseProtections.InstallAsync(_context.Database, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    private PropertySaveBehavior AfterSave<TEntity>(string propertyName) where TEntity : class =>
        Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetAfterSaveBehavior();

    private string[] UniqueIndexes<TEntity>() where TEntity : class => Model.FindEntityType(typeof(TEntity))!.GetIndexes()
        .Where(index => index.IsUnique).Select(index => string.Join(",", index.Properties.Select(property => property.Name)))
        .Order().ToArray();

    private IModel Model => _context.GetService<IDesignTimeModel>().Model;

    private InovaitDbContext CreateContext() => new(new DbContextOptionsBuilder<InovaitDbContext>()
        .UseSqlServer(_context.Database.GetConnectionString()).Options);

    private Task ClearCatalogsAsync() => _context.Database.ExecuteSqlRawAsync(
        "DISABLE TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete] ON [catalog].[AcademicConfiguration]; DELETE FROM [catalog].[AcademicConfiguration]; ENABLE TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete] ON [catalog].[AcademicConfiguration]; DELETE FROM [catalog].[DocumentType]; DELETE FROM [catalog].[Grade]; DELETE FROM [catalog].[School]; DELETE FROM [catalog].[AcademicYear]",
        TestContext.Current.CancellationToken);

    private static Task<int> CanonicalSeedCountAsync(InovaitDbContext context, CancellationToken cancellationToken) =>
        context.Database.SqlQueryRaw<int>(CanonicalSeedSql).SingleAsync(cancellationToken);

    private WebApplicationFactory<Program> CreateProductionFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:InovaitDatabase", _context.Database.GetConnectionString());
        });

    private async Task AssertProtectedUpdate(string command, int errorNumber)
    {
        var exception = await Assert.ThrowsAsync<SqlException>(() => _context.Database.ExecuteSqlRawAsync(
            command, TestContext.Current.CancellationToken));
        Assert.Equal(errorNumber, exception.Number);
    }

    private async Task AssertRuntimeWriteDenied(string command)
    {
        var sql = $"BEGIN TRY EXECUTE AS USER=N'inovait_runtime_test'; {command}; REVERT; END TRY BEGIN CATCH IF USER_NAME()=N'inovait_runtime_test' REVERT; THROW; END CATCH";
        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            _context.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken));
        Assert.Equal(229, exception.Number);
    }
}
