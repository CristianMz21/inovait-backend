using Inovait.Core.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subject", "catalog", table =>
        {
            table.HasTrigger("TR_Subject_ProtectCode");
            table.HasCheckConstraint("CK_Subject_Code_NotBlank", "LEN(TRIM([Code])) > 0");
            table.HasCheckConstraint("CK_Subject_Name_NotBlank", "LEN(TRIM([Name])) > 0");
            table.HasCheckConstraint("CK_Subject_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_Subject");
        var code = builder.Property(entity => entity.Code).HasColumnType("varchar(20)")
            .HasMaxLength(20).UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.Property(entity => entity.Name).HasColumnType("nvarchar(120)").HasMaxLength(120)
            .UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName("UQ_Subject_Code");
        builder.HasIndex(entity => entity.Name).IsUnique().HasDatabaseName("UQ_Subject_Name");
        CatalogConfigurationConventions.ProtectAfterSave(code);
        CatalogConfigurationConventions.ConfigureAudit(builder);
    }
}
