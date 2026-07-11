using Inovait.Core.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inovait.Infrastructure.Persistence.Interceptors;

public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    public AuditSaveChangesInterceptor(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditPolicy(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditPolicy(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditPolicy(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var updatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var modifiedEntries = context.ChangeTracker
            .Entries<IAuditableEntity>()
            .Where(entry => entry.State == EntityState.Modified)
            .ToArray();

        foreach (var entry in modifiedEntries)
        {
            var createdAt = entry.Property(entity => entity.CreatedAtUtc);
            createdAt.CurrentValue = createdAt.OriginalValue;
            createdAt.IsModified = false;

            var updatedAt = entry.Property(entity => entity.UpdatedAtUtc);
            updatedAt.CurrentValue = updatedAtUtc;
            updatedAt.IsModified = true;
        }
    }
}
