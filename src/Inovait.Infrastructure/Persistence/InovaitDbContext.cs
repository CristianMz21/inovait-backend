using Microsoft.EntityFrameworkCore;

namespace Inovait.Infrastructure.Persistence;

public sealed class InovaitDbContext(DbContextOptions<InovaitDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InovaitDbContext).Assembly);
    }
}
