using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.People;
using Inovait.Core.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class TeacherContractConfiguration : IEntityTypeConfiguration<TeacherContract>
{
    public void Configure(EntityTypeBuilder<TeacherContract> builder)
    {
        builder.ToTable("TeacherContract", "staff", table =>
        {
            table.HasCheckConstraint("CK_TeacherContract_DateRange", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
            table.HasCheckConstraint("CK_TeacherContract_Status_NotBlank", "LEN(TRIM([Status])) > 0");
            table.HasCheckConstraint("CK_TeacherContract_Status", "[Status] IN ('Confirmed','Cancelled')");
            table.HasCheckConstraint("CK_TeacherContract_StatusCancellation", "([Status]='Confirmed' AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL AND [CancellationEffectiveDate] IS NULL) OR ([Status]='Cancelled' AND [CancelledAtUtc] IS NOT NULL AND [CancellationReason] IS NOT NULL AND [CancellationEffectiveDate] IS NOT NULL)");
            table.HasCheckConstraint("CK_TeacherContract_CancellationReason_NotBlank", "[CancellationReason] IS NULL OR LEN(TRIM([CancellationReason])) > 0");
            table.HasCheckConstraint("CK_TeacherContract_CancellationEffectiveDate", "[CancellationEffectiveDate] IS NULL OR ([CancellationEffectiveDate] >= [StartDate] AND ([EndDate] IS NULL OR [CancellationEffectiveDate] <= [EndDate]))");
            table.HasCheckConstraint("CK_TeacherContract_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_TeacherContract");
        builder.Property(entity => entity.TeacherPersonId).HasColumnType("int");
        builder.Property(entity => entity.SchoolId).HasColumnType("int");
        builder.Property(entity => entity.StartDate).HasColumnType("date");
        builder.Property(entity => entity.EndDate).HasColumnType("date");
        builder.Property(entity => entity.Status).HasConversion<string>().HasColumnType("varchar(10)").HasMaxLength(10).IsRequired();
        builder.Property(entity => entity.CancelledAtUtc).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.CancellationReason).HasColumnType("nvarchar(300)").HasMaxLength(300)
            .UseCollation(CatalogConfigurationConventions.Collation);
        builder.Property(entity => entity.CancellationEffectiveDate).HasColumnType("date");
        builder.HasIndex(entity => new { entity.TeacherPersonId, entity.SchoolId, entity.StartDate, entity.EndDate })
            .IsUnique().HasFilter(null).HasDatabaseName("UQ_TeacherContract_Exact");
        builder.HasIndex(entity => new { entity.TeacherPersonId, entity.StartDate, entity.EndDate })
            .IncludeProperties(entity => new
            {
                entity.SchoolId,
                entity.Status,
                entity.CancelledAtUtc,
                entity.CancellationReason,
                entity.CancellationEffectiveDate
            })
            .HasDatabaseName("IX_TeacherContract_TeacherPersonId_StartDate_EndDate");
        builder.HasIndex(entity => new { entity.SchoolId, entity.StartDate, entity.EndDate })
            .IncludeProperties(entity => new { entity.TeacherPersonId, entity.Status, entity.CancellationEffectiveDate })
            .HasDatabaseName("IX_TeacherContract_SchoolId_StartDate_EndDate");
        builder.HasOne<Teacher>().WithMany().HasForeignKey(entity => entity.TeacherPersonId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TeacherContract_Teacher");
        builder.HasOne<School>().WithMany().HasForeignKey(entity => entity.SchoolId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TeacherContract_School");
        CatalogConfigurationConventions.ConfigureAudit(builder);
    }
}
