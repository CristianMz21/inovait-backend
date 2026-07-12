using Inovait.Core.Domain.Academics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class ClassScheduleConfiguration : IEntityTypeConfiguration<ClassSchedule>
{
    public void Configure(EntityTypeBuilder<ClassSchedule> builder)
    {
        builder.ToTable("ClassSchedule", "academic", table =>
        {
            table.HasCheckConstraint("CK_ClassSchedule_Weekday", "[Weekday] BETWEEN 1 AND 7");
        });
        builder.HasKey(entity => new { entity.TeachingAssignmentId, entity.Weekday }).HasName("PK_ClassSchedule");
        builder.Property(entity => entity.TeachingAssignmentId).HasColumnType("int");
        builder.Property(entity => entity.Weekday).HasColumnType("tinyint");
        var created = builder.Property(entity => entity.CreatedAtUtc).HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedOnAdd();
        created.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        builder.HasOne<TeachingAssignment>().WithMany().HasForeignKey(entity => entity.TeachingAssignmentId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ClassSchedule_TeachingAssignment");
    }
}
