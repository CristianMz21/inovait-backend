using Inovait.Infrastructure.Persistence.Interceptors;
using Inovait.Infrastructure.Text;
using Microsoft.EntityFrameworkCore;

namespace Inovait.UnitTests.Domain;

[Trait("Priority", "P0")]
[Trait("Evidence", "UT-TEXT-NORMALIZATION")]
public sealed class TextNormalizationInterceptorTests
{
    [Fact]
    public void AddedRequiredText_IsNormalizedBeforeSave()
    {
        var entity = new TextProbe { RequiredText = "\u2003Jose\u0301\t Pérez\u00A0" };

        using var context = CreateContext();
        context.Add(entity);
        context.SaveChanges();

        Assert.Equal("José Pérez", entity.RequiredText);
    }

    [Fact]
    public void AddedRequiredWhitespaceOnlyText_IsRejectedBeforeSave()
    {
        var entity = new TextProbe { RequiredText = " \t\n\u2003" };

        using var context = CreateContext();
        context.Add(entity);

        Assert.Throws<ArgumentException>(() => context.SaveChanges());
    }

    [Fact]
    public async Task ModifiedRequiredText_IsNormalizedBeforeAsyncSave()
    {
        var entity = new TextProbe { RequiredText = "Original" };

        await using var context = CreateContext();
        context.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        entity.RequiredText = "\n María\t José \u00A0";

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("María José", entity.RequiredText);
    }

    private static TextDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TextDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new TextNormalizationInterceptor(new TextNormalizer()))
            .Options;

        return new TextDbContext(options);
    }

    private sealed class TextDbContext(DbContextOptions<TextDbContext> options) : DbContext(options)
    {
        public DbSet<TextProbe> Probes => Set<TextProbe>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TextProbe>().Property(probe => probe.RequiredText).IsRequired();
        }
    }

    private sealed class TextProbe
    {
        public int Id { get; set; }

        public string RequiredText { get; set; } = string.Empty;
    }
}
