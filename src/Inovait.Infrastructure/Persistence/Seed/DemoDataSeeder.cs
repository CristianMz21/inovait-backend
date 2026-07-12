using Microsoft.EntityFrameworkCore;

namespace Inovait.Infrastructure.Persistence.Seed;

/// <summary>
/// Applies the fictitious LOCAL-EVALUATION demo dataset (24 students, 4 schools, 8 teachers with
/// active/simultaneous/expired/future contracts, 40 historical enrollments) that evaluators need to
/// exercise every endpoint and all five report questions. This is strictly a Development/local
/// evaluation aid -- it is NEVER part of the production seed and must never run unattended against a
/// production connection string (see the explicit opt-in flags in <c>Program.cs</c>).
/// </summary>
/// <remarks>
/// The executed T-SQL is read from an embedded resource that is a linked copy of
/// <c>database/seed-demo.sql</c> (see the <c>EmbeddedResource</c> entry in
/// <c>Inovait.Infrastructure.csproj</c>) -- there is exactly one physical copy of this script on
/// disk, so the standalone <c>sqlcmd</c> path and this in-process path can never diverge. The script
/// manages its own <c>BEGIN TRANSACTION</c>/<c>COMMIT</c>/<c>ROLLBACK</c> (mirroring
/// <see cref="ProductionCatalogSeed.ApplyAsync"/>), so it is executed directly via
/// <see cref="RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,string,System.Threading.CancellationToken)"/>
/// with no additional outer transaction.
/// </remarks>
public static class DemoDataSeeder
{
    private const string ResourceSuffix = "seed-demo.sql";

    public static async Task ApplyAsync(InovaitDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var script = await LoadScriptAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(script, cancellationToken);
    }

    private static async Task<string> LoadScriptAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(DemoDataSeeder).Assembly;
        var resourceName = Array.Find(assembly.GetManifestResourceNames(),
            name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        if (resourceName is null)
        {
            throw new InvalidOperationException(
                $"Embedded resource '{ResourceSuffix}' was not found in assembly '{assembly.FullName}'.");
        }

        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource stream '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
