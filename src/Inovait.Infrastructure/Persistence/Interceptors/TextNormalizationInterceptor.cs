using Inovait.Core.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inovait.Infrastructure.Persistence.Interceptors;

public sealed class TextNormalizationInterceptor : SaveChangesInterceptor
{
    private readonly ITextNormalizer _normalizer;

    public TextNormalizationInterceptor(ITextNormalizer normalizer)
    {
        ArgumentNullException.ThrowIfNull(normalizer);
        _normalizer = normalizer;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        NormalizeTrackedText(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        NormalizeTrackedText(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void NormalizeTrackedText(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var changedEntries = context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToArray();

        foreach (var entry in changedEntries)
        {
            var textProperties = entry.Properties
                .Where(property =>
                    property.Metadata.ClrType == typeof(string) &&
                    (entry.State == EntityState.Added || property.IsModified))
                .ToArray();

            foreach (var property in textProperties)
            {
                var value = (string?)property.CurrentValue;
                property.CurrentValue = property.Metadata.IsNullable
                    ? _normalizer.Normalize(value)
                    : _normalizer.NormalizeRequired(value);
            }
        }
    }
}
