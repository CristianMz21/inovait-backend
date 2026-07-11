using Inovait.Core.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

internal static class CatalogConfigurationConventions
{
    internal const string Collation = "Latin1_General_100_CI_AS";

    internal static void ConfigureAudit<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        ConfigureTimestamp(builder.Property(entity => entity.CreatedAtUtc));
        ConfigureTimestamp(builder.Property(entity => entity.UpdatedAtUtc));
        builder.Property(entity => entity.RowVersion).IsRowVersion();
    }

    internal static void ProtectAfterSave(PropertyBuilder property) =>
        property.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

    private static void ConfigureTimestamp(PropertyBuilder<DateTime> property)
    {
        property.HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()")
            .ValueGeneratedOnAdd();
        property.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
    }
}
