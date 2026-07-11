using Microsoft.EntityFrameworkCore;

namespace Inovait.Infrastructure.Persistence;

public sealed class AcademicConfigurationStartupCheck(InovaitDbContext context)
{
    public async Task EnsurePresentAsync(CancellationToken cancellationToken = default)
    {
        var exists = await context.AcademicConfigurations.AsNoTracking()
            .AnyAsync(configuration => configuration.Id == 1, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException(
                "Required catalog.AcademicConfiguration(Id=1) is missing; startup cannot continue.");
        }
    }
}
