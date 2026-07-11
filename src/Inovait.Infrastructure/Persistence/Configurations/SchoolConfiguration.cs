using Inovait.Core.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.ToTable("School", "catalog", table =>
        {
            table.HasTrigger("TR_School_ProtectStableValues");
            table.HasCheckConstraint("CK_School_Code_NotBlank", "LEN(TRIM([Code])) > 0");
            table.HasCheckConstraint("CK_School_Name_NotBlank", "LEN(TRIM([Name])) > 0");
            table.HasCheckConstraint("CK_School_Sector_NotBlank", "LEN(TRIM([Sector])) > 0");
            table.HasCheckConstraint("CK_School_Sector", "[Sector] IN ('Public','Private')");
            table.HasCheckConstraint("CK_School_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_School");
        var code = builder.Property(entity => entity.Code).HasColumnType("varchar(20)")
            .HasMaxLength(20).UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        var sector = builder.Property(entity => entity.Sector).HasConversion<string>()
            .HasColumnType("varchar(8)").HasMaxLength(8).UseCollation(CatalogConfigurationConventions.Collation);
        builder.Property(entity => entity.Name).HasColumnType("nvarchar(160)").HasMaxLength(160)
            .UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName("UQ_School_Code");
        builder.HasIndex(entity => entity.Name).IsUnique().HasDatabaseName("UQ_School_Name");
        CatalogConfigurationConventions.ProtectAfterSave(code);
        CatalogConfigurationConventions.ProtectAfterSave(sector);
        CatalogConfigurationConventions.ConfigureAudit(builder);
    }
}
