using Inovait.Core.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("DocumentType", "catalog", table =>
        {
            table.HasCheckConstraint("CK_DocumentType_Code_NotBlank", "LEN(TRIM([Code])) > 0");
            table.HasCheckConstraint("CK_DocumentType_Name_NotBlank", "LEN(TRIM([Name])) > 0");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_DocumentType");
        builder.Property(entity => entity.Id).HasColumnType("smallint");
        builder.Property(entity => entity.Code).HasColumnType("varchar(20)").HasMaxLength(20)
            .UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.Property(entity => entity.Name).HasColumnType("nvarchar(80)").HasMaxLength(80)
            .UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.Property(entity => entity.IsActive).HasColumnType("bit");
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName("UQ_DocumentType_Code");
    }
}
