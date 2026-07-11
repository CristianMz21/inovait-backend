using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class ClassGroupConfiguration : IEntityTypeConfiguration<ClassGroup>
{
    public void Configure(EntityTypeBuilder<ClassGroup> builder)
    {
        builder.ToTable("ClassGroup", "academic", table =>
        {
            table.HasCheckConstraint("CK_ClassGroup_Code_NotBlank", "LEN(TRIM([Code])) > 0");
            table.HasCheckConstraint("CK_ClassGroup_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_ClassGroup");
        builder.Property(entity => entity.SchoolId).HasColumnType("int");
        builder.Property(entity => entity.AcademicYearId).HasColumnType("int");
        builder.Property(entity => entity.GradeId).HasColumnType("int");
        builder.Property(entity => entity.Code).HasColumnType("varchar(20)").HasMaxLength(20)
            .UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.HasAlternateKey(entity => new { entity.Id, entity.AcademicYearId })
            .HasName("UQ_ClassGroup_Id_AcademicYear_ForEnrollment");
        builder.HasIndex(entity => new { entity.SchoolId, entity.AcademicYearId, entity.GradeId, entity.Code })
            .IsUnique().HasDatabaseName("UQ_ClassGroup_Context");
        builder.HasIndex(entity => new { entity.AcademicYearId, entity.GradeId, entity.SchoolId })
            .IncludeProperties(entity => entity.Code)
            .HasDatabaseName("IX_ClassGroup_AcademicYearId_GradeId_SchoolId");
        builder.HasIndex(entity => entity.GradeId).HasDatabaseName("IX_ClassGroup_GradeId");
        builder.HasOne<School>().WithMany().HasForeignKey(entity => entity.SchoolId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ClassGroup_School");
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(entity => entity.AcademicYearId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ClassGroup_AcademicYear");
        builder.HasOne<Grade>().WithMany().HasForeignKey(entity => entity.GradeId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ClassGroup_Grade");
        CatalogConfigurationConventions.ConfigureAudit(builder);
    }
}
