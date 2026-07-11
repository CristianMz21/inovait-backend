using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.Common;
using Inovait.Core.Domain.People;
using Inovait.Core.Domain.Staff;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Persistence;
using Inovait.Infrastructure.Persistence.Interceptors;
using Inovait.Infrastructure.Text;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Inovait.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P0")]
public sealed class AuditConcurrencyTests(SqlServerFixture fixture)
{
    private static readonly DateTime UpdateTimestamp = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProductionRegistrations_ResolveAndConnectToSqlServer()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        Assert.IsType<TextNormalizer>(scope.ServiceProvider.GetRequiredService<ITextNormalizer>());
        Assert.IsType<TextNormalizationInterceptor>(
            scope.ServiceProvider.GetRequiredService<TextNormalizationInterceptor>());
        Assert.IsType<AuditSaveChangesInterceptor>(
            scope.ServiceProvider.GetRequiredService<AuditSaveChangesInterceptor>());

        var context = scope.ServiceProvider.GetRequiredService<InovaitDbContext>();
        Assert.True(await context.Database.CanConnectAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProbeModel_UsesDefaultsCheckRealUpdatesAndRowVersion()
    {
        await using var provider = CreateProvider();
        var databaseName = $"InovaitS02_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = databaseName,
        }.ConnectionString;

        try
        {
            await using var setup = CreateProbeContext(provider, connectionString);
            await setup.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            var probe = new AuditProbe { Label = "\t Initial\n value \u2003" };
            setup.Add(probe);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Initial value", probe.Label);
            Assert.NotEqual(default, probe.CreatedAtUtc);
            Assert.True(probe.UpdatedAtUtc >= probe.CreatedAtUtc);
            Assert.NotEmpty(probe.RowVersion);

            var createdAtUtc = probe.CreatedAtUtc;
            var originalVersion = probe.RowVersion.ToArray();
            probe.Label = "updated";
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(createdAtUtc, probe.CreatedAtUtc);
            Assert.True(probe.UpdatedAtUtc > createdAtUtc);
            Assert.NotEqual(originalVersion, probe.RowVersion);

            await AssertCheckConstraintRejectsInvalidAuditOrder(setup);
            await AssertRowVersionRejectsStaleUpdate(provider, connectionString, probe.Id);
        }
        finally
        {
            await using var cleanup = CreateProbeContext(provider, connectionString);
            await cleanup.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    [Trait("Evidence", "IT-AUDIT-UTC-P0")]
    public async Task P0AuditAllocation_HasExactDefaultsChecksAndRealUtcUpdates()
    {
        var connectionString = CreateDatabaseConnection("Audit");
        await using var provider = CreateMigrationProvider(connectionString);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InovaitDbContext>();
        try
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(ExpectedAuditedTables, await context.Database.SqlQueryRaw<string>(AuditTableSql)
                .ToArrayAsync(TestContext.Current.CancellationToken));
            Assert.Equal(["academic.Enrollment"], await context.Database.SqlQueryRaw<string>(
                "SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name]) AS [Value] FROM sys.tables t WHERE EXISTS (SELECT 1 FROM sys.columns c JOIN sys.default_constraints d ON d.[object_id]=c.[default_object_id] WHERE c.[object_id]=t.[object_id] AND c.[name]='CreatedAtUtc') AND NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.[object_id]=t.[object_id] AND c.[name] IN ('UpdatedAtUtc','RowVersion')) ORDER BY [Value]")
                .ToArrayAsync(TestContext.Current.CancellationToken));
            Assert.Equal(ExpectedNonAuditedTables, await context.Database.SqlQueryRaw<string>(
                "SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name]) AS [Value] FROM sys.tables t WHERE t.[name] IN ('DocumentType','Student','AcademicConfiguration') AND NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.[object_id]=t.[object_id] AND c.[name] IN ('CreatedAtUtc','UpdatedAtUtc','RowVersion')) ORDER BY [Value]")
                .ToArrayAsync(TestContext.Current.CancellationToken));

            var entities = await SeedAuditGraphAsync(context);
            var created = entities.ToDictionary(entity => entity, entity => ((IAuditableEntity)entity).CreatedAtUtc);
            foreach (var entity in entities)
                context.Entry(entity).Property(nameof(IAuditableEntity.UpdatedAtUtc)).IsModified = true;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.All(entities, entity =>
            {
                var audited = (IAuditableEntity)entity;
                Assert.Equal(created[entity], audited.CreatedAtUtc);
                Assert.Equal(UpdateTimestamp, audited.UpdatedAtUtc);
            });
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    [Trait("Evidence", "IT-ROWVERSION-P0")]
    public async Task P0RowVersionAllocation_RejectsStaleUpdatesForExactlySevenEntities()
    {
        var connectionString = CreateDatabaseConnection("RowVersion");
        await using var provider = CreateMigrationProvider(connectionString);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InovaitDbContext>();
        try
        {
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
            var entities = await SeedAuditGraphAsync(context);
            var keys = entities.Select(entity => (entity.GetType(), context.Entry(entity).Properties
                .Where(property => property.Metadata.IsPrimaryKey()).Select(property => property.CurrentValue!).ToArray())).ToArray();
            context.ChangeTracker.Clear();
            foreach (var (entityType, key) in keys)
                await AssertStaleUpdateAsync(connectionString, entityType, key);

            Assert.Equal(ExpectedNonRowVersionTables, await context.Database.SqlQueryRaw<string>(
                "SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name]) AS [Value] FROM sys.tables t WHERE t.[name] IN ('Enrollment','DocumentType','Student','AcademicConfiguration') AND NOT EXISTS (SELECT 1 FROM sys.columns c WHERE c.[object_id]=t.[object_id] AND c.[name]='RowVersion') ORDER BY [Value]")
                .ToArrayAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
    }

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(
            new FixedTimeProvider(DateTimeOffset.UtcNow.AddMinutes(10)));
        services.AddInovaitInfrastructure(fixture.ConnectionString);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private ServiceProvider CreateMigrationProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(UpdateTimestamp));
        services.AddInovaitInfrastructure(connectionString);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private string CreateDatabaseConnection(string suffix) => new SqlConnectionStringBuilder(fixture.ConnectionString)
    {
        InitialCatalog = $"InovaitS07{suffix}_{Guid.NewGuid():N}",
    }.ConnectionString;

    private static async Task<object[]> SeedAuditGraphAsync(InovaitDbContext context)
    {
        var person = new Person(1, $"{Guid.NewGuid():N}", "Audit", "Evidence", new(1990, 1, 1));
        context.Add(person);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var teacher = new Teacher(person.Id);
        var group = new ClassGroup(1, 1, 1, $"G-{Guid.NewGuid():N}"[..20]);
        context.AddRange(teacher, group);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var contract = new TeacherContract(teacher.PersonId, 1, new(2026, 1, 1), new(2026, 12, 31));
        context.Add(contract);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return [await context.Schools.SingleAsync(TestContext.Current.CancellationToken),
            await context.AcademicYears.SingleAsync(TestContext.Current.CancellationToken),
            await context.Grades.SingleAsync(TestContext.Current.CancellationToken), group, person, teacher, contract];
    }

    private static async Task AssertStaleUpdateAsync(string connectionString, Type entityType, object[] key)
    {
        var options = new DbContextOptionsBuilder<InovaitDbContext>().UseSqlServer(connectionString).Options;
        await using var first = new InovaitDbContext(options);
        await using var stale = new InovaitDbContext(options);
        var firstCopy = (await first.FindAsync(entityType, key, TestContext.Current.CancellationToken))!;
        var staleCopy = (await stale.FindAsync(entityType, key, TestContext.Current.CancellationToken))!;
        first.Entry(firstCopy).Property(nameof(IAuditableEntity.UpdatedAtUtc)).CurrentValue = UpdateTimestamp.AddMinutes(1);
        stale.Entry(staleCopy).Property(nameof(IAuditableEntity.UpdatedAtUtc)).CurrentValue = UpdateTimestamp.AddMinutes(2);
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static ProbeDbContext CreateProbeContext(
        IServiceProvider provider,
        string connectionString)
    {
        var options = new DbContextOptionsBuilder<ProbeDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(
                provider.GetRequiredService<TextNormalizationInterceptor>(),
                provider.GetRequiredService<AuditSaveChangesInterceptor>())
            .Options;

        return new ProbeDbContext(options);
    }

    private static async Task AssertCheckConstraintRejectsInvalidAuditOrder(
        ProbeDbContext context)
    {
        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO [s02].[AuditProbe] ([Label], [CreatedAtUtc], [UpdatedAtUtc])
                VALUES (N'invalid', '2026-07-11T12:00:00', '2026-07-11T11:59:59');
                """,
                TestContext.Current.CancellationToken));

        Assert.Equal(547, exception.Number);
    }

    private static async Task AssertRowVersionRejectsStaleUpdate(
        IServiceProvider provider,
        string connectionString,
        int id)
    {
        await using var first = CreateProbeContext(provider, connectionString);
        await using var stale = CreateProbeContext(provider, connectionString);
        var firstCopy = await first.Probes.SingleAsync(
            probe => probe.Id == id,
            TestContext.Current.CancellationToken);
        var staleCopy = await stale.Probes.SingleAsync(
            probe => probe.Id == id,
            TestContext.Current.CancellationToken);

        firstCopy.Label = "first writer";
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        staleCopy.Label = "stale writer";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            stale.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options)
        : DbContext(options)
    {
        public DbSet<AuditProbe> Probes => Set<AuditProbe>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<AuditProbe>();
            entity.ToTable(
                "AuditProbe",
                "s02",
                table => table.HasCheckConstraint(
                    "CK_AuditProbe_AuditOrder",
                    "[UpdatedAtUtc] >= [CreatedAtUtc]"));
            entity.Property(probe => probe.Label).HasMaxLength(100).IsRequired();
            ConfigureGeneratedTimestamp(entity.Property(probe => probe.CreatedAtUtc));
            ConfigureGeneratedTimestamp(entity.Property(probe => probe.UpdatedAtUtc));
            entity.Property(probe => probe.RowVersion).IsRowVersion();
        }

        private static void ConfigureGeneratedTimestamp(
            Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime> property)
        {
            property.HasColumnType("datetime2(3)")
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAdd();
            property.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        }
    }

    private sealed class AuditProbe : IAuditableEntity
    {
        public int Id { get; set; }

        public string Label { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; private set; }

        public DateTime UpdatedAtUtc { get; private set; }

        public byte[] RowVersion { get; private set; } = [];
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private const string AuditTableSql = "SELECT CONCAT(SCHEMA_NAME(t.[schema_id]),'.',t.[name]) AS [Value] FROM sys.tables t WHERE (SELECT COUNT(*) FROM sys.columns c JOIN sys.default_constraints d ON d.[object_id]=c.[default_object_id] WHERE c.[object_id]=t.[object_id] AND c.[name] IN ('CreatedAtUtc','UpdatedAtUtc'))=2 AND EXISTS (SELECT 1 FROM sys.columns c WHERE c.[object_id]=t.[object_id] AND c.[name]='RowVersion' AND c.[system_type_id]=189) AND EXISTS (SELECT 1 FROM sys.check_constraints ck WHERE ck.[parent_object_id]=t.[object_id] AND ck.[name]=CONCAT('CK_',t.[name],'_UpdatedAtUtc')) ORDER BY [Value]";
    private static readonly string[] ExpectedAuditedTables = ["academic.ClassGroup", "catalog.AcademicYear", "catalog.Grade", "catalog.School", "people.Person", "people.Teacher", "staff.TeacherContract"];
    private static readonly string[] ExpectedNonAuditedTables = ["catalog.AcademicConfiguration", "catalog.DocumentType", "people.Student"];
    private static readonly string[] ExpectedNonRowVersionTables = ["academic.Enrollment", "catalog.AcademicConfiguration", "catalog.DocumentType", "people.Student"];
}
