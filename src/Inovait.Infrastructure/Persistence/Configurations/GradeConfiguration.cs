using Inovait.Core.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.ToTable("Grade", "catalog", table =>
        {
            table.HasTrigger("TR_Grade_ProtectCode");
            table.HasCheckConstraint("CK_Grade_Code_NotBlank", "LEN(TRIM([Code])) > 0");
            table.HasCheckConstraint("CK_Grade_Name_NotBlank", "LEN(TRIM([Name])) > 0");
            table.HasCheckConstraint("CK_Grade_SortOrder", "[SortOrder] > 0");
            table.HasCheckConstraint("CK_Grade_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_Grade");
        var code = builder.Property(entity => entity.Code).HasColumnType("varchar(20)")
            .HasMaxLength(20).UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.Property(entity => entity.Name).HasColumnType("nvarchar(80)").HasMaxLength(80)
            .UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.Property(entity => entity.SortOrder).HasColumnType("smallint");
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName("UQ_Grade_Code");
        builder.HasIndex(entity => entity.Name).IsUnique().HasDatabaseName("UQ_Grade_Name");
        builder.HasIndex(entity => entity.SortOrder).IsUnique().HasDatabaseName("UQ_Grade_SortOrder");
        CatalogConfigurationConventions.ProtectAfterSave(code);
        CatalogConfigurationConventions.ConfigureAudit(builder);
    }
}
