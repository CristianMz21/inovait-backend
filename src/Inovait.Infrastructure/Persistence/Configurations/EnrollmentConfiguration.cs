using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollment", "academic");
        builder.HasKey(entity => entity.Id).HasName("PK_Enrollment");
        builder.Property(entity => entity.StudentPersonId).HasColumnType("int");
        builder.Property(entity => entity.ClassGroupId).HasColumnType("int");
        builder.Property(entity => entity.AcademicYearId).HasColumnType("int");
        var created = builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
        created.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        builder.HasIndex(entity => new { entity.StudentPersonId, entity.AcademicYearId }).IsUnique()
            .HasDatabaseName("UQ_Enrollment_StudentPersonId_AcademicYearId");
        builder.HasIndex(entity => new { entity.ClassGroupId, entity.StudentPersonId })
            .IncludeProperties(entity => new { entity.AcademicYearId, entity.CreatedAtUtc })
            .HasDatabaseName("IX_Enrollment_ClassGroupId_StudentPersonId");
        builder.HasOne<Student>().WithMany().HasForeignKey(entity => entity.StudentPersonId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Enrollment_Student");
        builder.HasOne<ClassGroup>().WithMany()
            .HasForeignKey(entity => new { entity.ClassGroupId, entity.AcademicYearId })
            .HasPrincipalKey(entity => new { entity.Id, entity.AcademicYearId })
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Enrollment_ClassGroupId_AcademicYearId");
    }
}
