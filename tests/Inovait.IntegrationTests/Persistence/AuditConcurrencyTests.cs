using Inovait.Core.Domain.Common;
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

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(
            new FixedTimeProvider(DateTimeOffset.UtcNow.AddMinutes(10)));
        services.AddInovaitInfrastructure(fixture.ConnectionString);
        return services.BuildServiceProvider(validateScopes: true);
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
}
