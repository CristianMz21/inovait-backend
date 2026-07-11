using Inovait.Core.Domain.Catalogs;
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
public sealed class CatalogModelTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;

    [Fact]
    [Trait("Evidence", "IT-CATALOG-SCHEMA-S03")]
    public async Task CatalogSchema_UsesCanonicalPhysicalMappings()
    {
        var tables = await _context.Database.SqlQueryRaw<string>(
            "SELECT CONCAT(SCHEMA_NAME([schema_id]), '.', [name]) AS [Value] FROM sys.tables ORDER BY [name]")
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
        Assert.Equal("SCH-01", (await _context.Schools.SingleAsync(TestContext.Current.CancellationToken)).Code);
    }

    [Fact]
    [Trait("Evidence", "IT-CATALOG-SINGLETON-S03")]
    public async Task AcademicConfiguration_UsesTinyIntSingletonCheckAndNoActionForeignKey()
    {
        var entity = Model.FindEntityType(typeof(AcademicConfiguration))!;
        Assert.Equal(DeleteBehavior.NoAction, Assert.Single(entity.GetForeignKeys()).DeleteBehavior);
        _context.Add(new AcademicYear("2026", "Academic year 2026", new(2026, 1, 1), new(2026, 12, 31)));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.Add(new AcademicConfiguration(2, 1));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(547, Assert.IsType<SqlException>(exception.InnerException).Number);
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
}
