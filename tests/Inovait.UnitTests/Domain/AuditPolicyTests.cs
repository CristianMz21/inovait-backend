using Inovait.Core.Domain.Common;
using Inovait.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Inovait.UnitTests.Domain;

[Trait("Priority", "P0")]
[Trait("Evidence", "UT-AUDIT-INTERCEPTOR")]
public sealed class AuditPolicyTests
{
    [Fact]
    public void AuditContract_DoesNotExposeTimestampSetters()
    {
        Assert.All(
            typeof(IAuditableEntity).GetProperties(),
            property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void AddedEntry_UsesGeneratedMetadataAndLeavesAuditValuesUnset()
    {
        var entity = new AuditProbe { Label = "new" };

        using var context = CreateContext(
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 10, 13, 0, 0, TimeSpan.Zero)));
        context.Add(entity);
        var createdAt = context.Entry(entity).Property(probe => probe.CreatedAtUtc);
        var updatedAt = context.Entry(entity).Property(probe => probe.UpdatedAtUtc);

        Assert.Equal(ValueGenerated.OnAdd, createdAt.Metadata.ValueGenerated);
        Assert.Equal(PropertySaveBehavior.Ignore, createdAt.Metadata.GetBeforeSaveBehavior());
        Assert.Equal("SYSUTCDATETIME()", createdAt.Metadata.GetDefaultValueSql());
        Assert.Equal(ValueGenerated.OnAdd, updatedAt.Metadata.ValueGenerated);
        Assert.Equal(PropertySaveBehavior.Ignore, updatedAt.Metadata.GetBeforeSaveBehavior());
        Assert.Equal("SYSUTCDATETIME()", updatedAt.Metadata.GetDefaultValueSql());

        context.SaveChanges();

        Assert.Equal(default, entity.CreatedAtUtc);
        Assert.Equal(default, entity.UpdatedAtUtc);
    }

    [Fact]
    public async Task ModifiedEntryAsync_PreservesCreationAndSetsUtcUpdateFromClock()
    {
        var createdAtUtc = new DateTime(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);
        var previousUpdateUtc = createdAtUtc.AddHours(1);
        var clockValue = new DateTimeOffset(2026, 7, 10, 18, 30, 0, TimeSpan.FromHours(-3));
        var entity = new AuditProbe
        {
            Id = 1,
            Label = "before",
        };

        await using var context = CreateContext(new FixedTimeProvider(clockValue));
        context.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        SetStoreGeneratedAuditValues(context, entity, createdAtUtc, previousUpdateUtc);
        context.Entry(entity).Property(probe => probe.CreatedAtUtc).CurrentValue = createdAtUtc.AddDays(1);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(createdAtUtc, entity.CreatedAtUtc);
        Assert.Equal(clockValue.UtcDateTime, entity.UpdatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, entity.UpdatedAtUtc.Kind);
    }

    [Fact]
    public void ModifiedEntry_SetsUpdateWhenCreationWasNotChanged()
    {
        var createdAtUtc = new DateTime(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);
        var clockValue = new DateTimeOffset(2026, 7, 10, 20, 0, 0, TimeSpan.Zero);
        var entity = new AuditProbe
        {
            Label = "before",
        };

        using var context = CreateContext(new FixedTimeProvider(clockValue));
        context.Add(entity);
        context.SaveChanges();
        SetStoreGeneratedAuditValues(context, entity, createdAtUtc, createdAtUtc);
        entity.Label = "after";

        context.SaveChanges();

        Assert.Equal(createdAtUtc, entity.CreatedAtUtc);
        Assert.Equal(clockValue.UtcDateTime, entity.UpdatedAtUtc);
    }

    private static void SetStoreGeneratedAuditValues(
        DbContext context,
        AuditProbe entity,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        var entry = context.Entry(entity);
        entry.State = EntityState.Unchanged;

        var createdAt = entry.Property(probe => probe.CreatedAtUtc);
        createdAt.CurrentValue = createdAtUtc;
        createdAt.OriginalValue = createdAtUtc;

        var updatedAt = entry.Property(probe => probe.UpdatedAtUtc);
        updatedAt.CurrentValue = updatedAtUtc;
        updatedAt.OriginalValue = updatedAtUtc;
    }

    private static AuditDbContext CreateContext(TimeProvider timeProvider)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditSaveChangesInterceptor(timeProvider))
            .Options;

        return new AuditDbContext(options);
    }

    private sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
    {
        public DbSet<AuditProbe> Probes => Set<AuditProbe>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<AuditProbe>();
            var createdAt = entity.Property(probe => probe.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAdd();
            var updatedAt = entity.Property(probe => probe.UpdatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .ValueGeneratedOnAdd();

            createdAt.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
            updatedAt.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        }
    }

    private sealed class AuditProbe : IAuditableEntity
    {
        public int Id { get; set; }

        public DateTime CreatedAtUtc { get; private set; }

        public DateTime UpdatedAtUtc { get; private set; }

        public string Label { get; set; } = string.Empty;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
