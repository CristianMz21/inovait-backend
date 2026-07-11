using Inovait.Core.Domain.Catalogs;
using Inovait.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace Inovait.Infrastructure.Persistence;

public sealed class InovaitDbContext(DbContextOptions<InovaitDbContext> options)
    : DbContext(options)
{
    public DbSet<School> Schools => Set<School>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<AcademicConfiguration> AcademicConfigurations => Set<AcademicConfiguration>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InovaitDbContext).Assembly);
        ProductionCatalogSeed.Apply(modelBuilder);
    }
}
