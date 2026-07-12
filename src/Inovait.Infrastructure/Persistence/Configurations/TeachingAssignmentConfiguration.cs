using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class TeachingAssignmentConfiguration : IEntityTypeConfiguration<TeachingAssignment>
{
    public void Configure(EntityTypeBuilder<TeachingAssignment> builder)
    {
        builder.ToTable("TeachingAssignment", "academic", table =>
        {
            table.HasCheckConstraint("CK_TeachingAssignment_DateRange", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
            table.HasCheckConstraint("CK_TeachingAssignment_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_TeachingAssignment");
        builder.Property(entity => entity.TeacherContractId).HasColumnType("int");
        builder.Property(entity => entity.ClassGroupId).HasColumnType("int");
        builder.Property(entity => entity.SubjectId).HasColumnType("int");
        builder.Property(entity => entity.StartDate).HasColumnType("date");
        builder.Property(entity => entity.EndDate).HasColumnType("date");
        builder.HasIndex(entity => new { entity.TeacherContractId, entity.ClassGroupId, entity.SubjectId })
            .IsUnique().HasDatabaseName("UQ_TeachingAssignment_Contract_Group_Subject");
        builder.HasIndex(entity => new { entity.ClassGroupId, entity.StartDate, entity.EndDate })
            .IncludeProperties(entity => new { entity.TeacherContractId, entity.SubjectId })
            .HasDatabaseName("IX_TeachingAssignment_ClassGroupId_StartDate_EndDate");
        builder.HasIndex(entity => new { entity.TeacherContractId, entity.StartDate, entity.EndDate })
            .IncludeProperties(entity => new { entity.ClassGroupId, entity.SubjectId })
            .HasDatabaseName("IX_TeachingAssignment_TeacherContractId_StartDate_EndDate");
        builder.HasIndex(entity => entity.SubjectId).HasDatabaseName("IX_TeachingAssignment_SubjectId");
        builder.HasOne<TeacherContract>().WithMany().HasForeignKey(entity => entity.TeacherContractId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TeachingAssignment_TeacherContract");
        builder.HasOne<ClassGroup>().WithMany().HasForeignKey(entity => entity.ClassGroupId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TeachingAssignment_ClassGroup");
        builder.HasOne<Subject>().WithMany().HasForeignKey(entity => entity.SubjectId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TeachingAssignment_Subject");
        CatalogConfigurationConventions.ConfigureAudit(builder);
    }
}
