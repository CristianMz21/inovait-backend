using Inovait.Core.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class AcademicConfigurationConfiguration : IEntityTypeConfiguration<AcademicConfiguration>
{
    public void Configure(EntityTypeBuilder<AcademicConfiguration> builder)
    {
        builder.ToTable("AcademicConfiguration", "catalog", table =>
            table.HasCheckConstraint("CK_AcademicConfiguration_Singleton", "[Id] = 1"));
        builder.HasKey(entity => entity.Id).HasName("PK_AcademicConfiguration");
        builder.Property(entity => entity.Id).HasColumnType("tinyint").ValueGeneratedNever();
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(entity => entity.CurrentAcademicYearId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_AcademicConfiguration_AcademicYear");
    }
}
