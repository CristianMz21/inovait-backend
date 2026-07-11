using Inovait.Core.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("AcademicYear", "catalog", table =>
        {
            table.HasCheckConstraint("CK_AcademicYear_Code_NotBlank", "LEN(TRIM([Code])) > 0");
            table.HasCheckConstraint("CK_AcademicYear_Name_NotBlank", "LEN(TRIM([Name])) > 0");
            table.HasCheckConstraint("CK_AcademicYear_DateRange", "[EndDate] >= [StartDate]");
            table.HasCheckConstraint("CK_AcademicYear_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_AcademicYear");
        var code = builder.Property(entity => entity.Code).HasColumnType("varchar(20)")
            .HasMaxLength(20).UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.Property(entity => entity.Name).HasColumnType("nvarchar(80)").HasMaxLength(80)
            .UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.Property(entity => entity.StartDate).HasColumnType("date");
        builder.Property(entity => entity.EndDate).HasColumnType("date");
        builder.HasIndex(entity => entity.Code).IsUnique().HasDatabaseName("UQ_AcademicYear_Code");
        builder.HasIndex(entity => entity.Name).IsUnique().HasDatabaseName("UQ_AcademicYear_Name");
        CatalogConfigurationConventions.ProtectAfterSave(code);
        CatalogConfigurationConventions.ConfigureAudit(builder);
    }
}
