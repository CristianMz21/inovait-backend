using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inovait.Infrastructure.Persistence;

public sealed class InovaitDbContextFactory : IDesignTimeDbContextFactory<InovaitDbContext>
{
    public InovaitDbContext CreateDbContext(string[] args)
    {
        // Design-time only (dotnet-ef migrations add); never opens a connection.
        var connectionString =
            Environment.GetEnvironmentVariable("INOVAIT_DESIGN_TIME_CONNECTION")
            ?? "Server=(local);Database=Inovait_DesignTime;Integrated Security=True";

        var options = new DbContextOptionsBuilder<InovaitDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new InovaitDbContext(options);
    }
}
