using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inovait.Infrastructure.Persistence;

public sealed class InovaitDbContextFactory : IDesignTimeDbContextFactory<InovaitDbContext>
{
    public InovaitDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("INOVAIT_DESIGN_TIME_CONNECTION")
            ?? "Server=(local);Database=Inovait_DesignTime;TrustServerCertificate=True;Encrypt=False;Integrated Security=True";

        var options = new DbContextOptionsBuilder<InovaitDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new InovaitDbContext(options);
    }
}
