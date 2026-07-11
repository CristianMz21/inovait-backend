using Inovait.Core.Domain.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("Teacher", "people", table =>
            table.HasCheckConstraint("CK_Teacher_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]"));
        builder.HasKey(entity => entity.PersonId).HasName("PK_Teacher");
        builder.Property(entity => entity.PersonId).HasColumnType("int").ValueGeneratedNever();
        builder.HasOne<Person>().WithOne().HasForeignKey<Teacher>(entity => entity.PersonId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Teacher_Person");
        CatalogConfigurationConventions.ConfigureAudit(builder);
    }
}
